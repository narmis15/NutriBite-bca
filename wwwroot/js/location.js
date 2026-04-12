 // location.js - handles automatic geolocation, reverse geocoding coordination and UI updates.
    // Requires Bootstrap (for modal) and that _LocationSelector partial/modal exists on the page.
    // Uses /Location/SaveLocation and /Location/GetCurrentLocation endpoints.

    (function () {
        const api = {
            getCurrent: '/Location/GetCurrentLocation',
            save: '/Location/SaveLocation',
            saveManual: '/Location/SaveLocationManual',
            search: (q) => `https://nominatim.openstreetmap.org/search?format=jsonv2&q=${encodeURIComponent(q)}&addressdetails=1&limit=6`
        };

        const elements = {
            display: null,         // element that displays current address in navbar
            modal: null,           // bootstrap modal
            btnDetectNow: null,
            locationLoader: null,
            detectedLocation: null,
            detectedAddress: null,
            confirmDetected: null,
            searchInput: null,
            searchResults: null
        };

        // Minimum required for Nominatim: delay between requests and a proper user-agent (server side already sets referer)
        let searchTimeout = null;
        const searchDebounceMs = 350;

        function init(config) {
            elements.display = document.getElementById(config.displayId || 'locationDisplay');
            elements.modal = new bootstrap.Modal(document.getElementById('locationModal'), { backdrop: 'static', keyboard: false });
            elements.btnDetectNow = document.getElementById('btnDetectNow');
            elements.locationLoader = document.getElementById('locationLoader');
            elements.detectedLocation = document.getElementById('detectedLocation');
            elements.detectedAddress = document.getElementById('detectedAddress');
            elements.confirmDetected = document.getElementById('confirmDetected');
            elements.searchInput = document.getElementById('locationSearchInput');
            elements.searchResults = document.getElementById('searchResults');

            // wire buttons
            if (elements.btnDetectNow) elements.btnDetectNow.addEventListener('click', detectAndSave);
            if (elements.confirmDetected) elements.confirmDetected.addEventListener('click', confirmDetectedLocation);
            if (elements.searchInput) elements.searchInput.addEventListener('input', onSearchInput);

            // initial read: server may have stored location in session
            fetch(api.getCurrent)
                .then(r => r.json())
                .then(data => {
                    if (data && data.success) {
                        const city = data.city || '';
                        const pincode = data.pincode || '';
                        const area = data.area || '';
                        const full = data.address || '';
                        updateNavbarLocationDisplay(full || (city + (pincode ? ' ' + pincode : '')));
                    } else {
                        // if no session, attempt auto-detect once (first-visit) but do not spam
                        // only trigger if page served over HTTPS or localhost
                        if (location.protocol === 'https:' || location.hostname === 'localhost') {
                            // small delay to avoid immediate prompt on page load
                            setTimeout(() => promptForLocationIfAutoAllowed(), 600);
                        } else {
                            // show a gentle prompt to open location modal for manual entry
                            updateNavbarLocationDisplay('Set your location');
                        }
                    }
                })
                .catch(() => {
                    // do nothing
                });
        }

        function promptForLocationIfAutoAllowed() {
            // Check if browser supports geolocation
            if (!('geolocation' in navigator)) {
                updateNavbarLocationDisplay('Location not supported');
                return;
            }

            // Only ask if permission is prompt (not denied). Use Permissions API if available.
            if (navigator.permissions && navigator.permissions.query) {
                navigator.permissions.query({ name: 'geolocation' }).then(status => {
                    if (status.state === 'granted') {
                        // directly detect
                        detectAndSave();
                    } else if (status.state === 'prompt') {
                        // show small modal to allow user to start detection
                        // open modal automatically so user can choose
                        if (elements.modal) elements.modal.show();
                    } else {
                        // denied: show manual prompt
                        updateNavbarLocationDisplay('Set your location');
                    }
                }).catch(() => {
                    // fallback: show modal
                    if (elements.modal) elements.modal.show();
                });
            } else {
                // no permissions API: show modal to give user control
                if (elements.modal) elements.modal.show();
            }
        }

        function updateNavbarLocationDisplay(text) {
            if (!elements.display) return;
            elements.display.innerHTML = `<span class="small text-muted">📍</span> <strong>${escapeHtml(text)}</strong>`;
        }

        function escapeHtml(s) {
            if (!s) return '';
            return s.replace(/[&<>"'`=\/]/g, function (c) {
                return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;', '/': '&#47;', '`': '&#96;', '=': '&#61;' }[c];
            });
        }

        let accuracyFailCount = 0;
        const MAX_ACCURACY_FAILS = 3;
        const TARGET_ACCURACY_METERS = 35;

        async function detectAndSave() {
            if (!navigator.geolocation) {
                showDetectError('Geolocation not supported by your browser.');
                return;
            }

            showLoader(true);
            elements.detectedLocation.style.display = 'none';

            // High precision options
            const options = { 
                enableHighAccuracy: true, 
                timeout: 15000, 
                maximumAge: 0 
            };

            let watchId = null;
            let bestPosition = null;
            let retryCount = 0;
            const maxRetries = 3;

            const getBestLocation = () => new Promise((resolve, reject) => {
                // Initial quick grab
                navigator.geolocation.getCurrentPosition(
                    (pos) => {
                        bestPosition = pos;
                        console.log("Initial position acquired:", pos.coords.accuracy);
                        if (pos.coords.accuracy <= TARGET_ACCURACY_METERS) {
                            resolve(pos);
                        }
                    },
                    (err) => console.warn("Initial position error:", err),
                    options
                );

                // Watch for improvements
                watchId = navigator.geolocation.watchPosition(
                    (pos) => {
                        console.log("Location update:", pos.coords.accuracy);
                        if (!bestPosition || pos.coords.accuracy < bestPosition.coords.accuracy) {
                            bestPosition = pos;
                        }
                        
                        // If we hit our target accuracy, stop watching and resolve
                        if (pos.coords.accuracy <= TARGET_ACCURACY_METERS) {
                            navigator.geolocation.clearWatch(watchId);
                            resolve(pos);
                        }
                    },
                    (err) => {
                        console.error("Watch error:", err);
                        if (bestPosition) resolve(bestPosition);
                        else reject(err);
                    },
                    options
                );

                // Safety timeout: after 10s, take the best we've found
                setTimeout(() => {
                    if (watchId) navigator.geolocation.clearWatch(watchId);
                    if (bestPosition) resolve(bestPosition);
                    else reject({ code: 3, message: "Location timeout" });
                }, 10000);
            });

            try {
                const pos = await getBestLocation();
                const lat = +pos.coords.latitude;
                const lon = +pos.coords.longitude;
                const accuracy = pos.coords.accuracy;

                console.log(`Best location resolved: ${lat}, ${lon} (Accuracy: ${accuracy}m)`);

                // Accuracy monitoring logic
                if (accuracy > TARGET_ACCURACY_METERS) {
                    accuracyFailCount++;
                    if (accuracyFailCount >= MAX_ACCURACY_FAILS) {
                        showAlertBootstrap('warning', `<i class="bi bi-exclamation-triangle-fill"></i> Accuracy is ${Math.round(accuracy)}m. Try moving near a window.`);
                        accuracyFailCount = 0;
                    }
                } else {
                    accuracyFailCount = 0;
                }

                const res = await fetch(api.save, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ latitude: lat, longitude: lon, accuracy: accuracy })
                });

                const payload = await res.json();
                if (!res.ok || !payload.success) {
                    showDetectError(payload?.message || 'Failed to locate. Try searching manually.');
                    return;
                }

                const location = payload.location || payload.location;
                const full = location?.FullAddress || location?.fullAddress || '';
                elements.detectedAddress.innerText = full;
                elements.detectedLocation.style.display = '';
                updateNavbarLocationDisplay(full || `${location?.City || ''} ${location?.Pincode || ''}`);
                setTimeout(() => elements.modal?.hide(), 900);

            } catch (err) {
                console.error("Final geolocation error:", err);
                if (err.code === 1) showDetectError('Location permission denied.');
                else if (err.code === 3) showDetectError('Location timeout. Try manually.');
                else showDetectError(err.message || 'Unable to detect location.');
            } finally {
                showLoader(false);
            }
        }

        function showLoader(visible) {
            if (!elements.locationLoader) return;
            elements.locationLoader.style.display = visible ? 'block' : 'none';
        }

        function showDetectError(msg) {
            showAlertBootstrap('danger', msg);
        }

        function showAlertBootstrap(type, html) {
            const root = elements.searchResults || document.body;
            const wrapper = document.createElement('div');
            wrapper.className = `alert alert-${type} mt-2`;
            wrapper.innerHTML = html;
            // replace previous
            if (elements.searchResults) elements.searchResults.innerHTML = '';
            root.prepend(wrapper);
            setTimeout(() => wrapper.remove(), 7000);
        }

        async function confirmDetectedLocation() {
            // server-side session already saved by SaveLocation call above; just update UI
            const res = await fetch(api.getCurrent);
            const payload = await res.json();
            if (payload && payload.success) {
                updateNavbarLocationDisplay(payload.address || payload.city || 'Selected location');
                elements.modal.hide();
            } else {
                showDetectError('Could not confirm location');
            }
        }

        // Manual search handling (forward geocoding via Nominatim)
        function onSearchInput(e) {
            const q = e.target.value;
            if (!q || q.trim().length < 2) {
                elements.searchResults.innerHTML = '';
                return;
            }
            if (searchTimeout) clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => performSearch(q.trim()), searchDebounceMs);
        }

        async function performSearch(query) {
            try {
                elements.searchResults.innerHTML = '<div class="text-center p-2 text-muted">Searching…</div>';
                const res = await fetch(api.search(query), { headers: { 'Accept': 'application/json' } });
                if (!res.ok) {
                    elements.searchResults.innerHTML = '<div class="text-danger p-2">Search failed</div>';
                    return;
                }
                const list = await res.json();
                renderSearchResults(list || []);
            } catch (err) {
                console.error(err);
                elements.searchResults.innerHTML = '<div class="text-danger p-2">Search failed</div>';
            }
        }

        function renderSearchResults(items) {
            if (!items || items.length === 0) {
                elements.searchResults.innerHTML = '<div class="text-muted p-2">No results</div>';
                return;
            }
            const html = items.map(it => {
                const display = it.display_name || `${it.address?.city || it.address?.town || ''}`;
                return `<button class="list-group-item list-group-item-action" data-lat="${it.lat}" data-lon="${it.lon}" data-display="${escapeHtml(display)}">
                        ${escapeHtml(display)}
                    </button>`;
            }).join('');
            elements.searchResults.innerHTML = html;

            // wire up click handlers
            elements.searchResults.querySelectorAll('button').forEach(b => {
                b.addEventListener('click', async function () {
                    const lat = parseFloat(this.dataset.lat);
                    const lon = parseFloat(this.dataset.lon);
                    const display = this.dataset.display;

                    // Save manual selection to session via dedicated endpoint
                    try {
                        const payload = {
                            latitude: lat,
                            longitude: lon,
                            city: (this.dataset.display || '').split(',').slice(-3, -2).join('').trim(),
                            area: (this.dataset.display || '').split(',')[0],
                            state: '',
                            pincode: '',
                            fullAddress: display
                        };
                        const res = await fetch(api.saveManual, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        });
                        const result = await res.json();
                        if (res.ok && result.success) {
                            updateNavbarLocationDisplay(display);
                            elements.modal.hide();
                        } else {
                            showAlertBootstrap('danger', 'Failed to save selected address');
                        }
                    } catch (err) {
                        console.error(err);
                        showAlertBootstrap('danger', 'Failed to save selected address');
                    }
                });
            });
        }

        // Expose initializer
        window.NutriBiteLocation = {
            init: init
        };
    })();