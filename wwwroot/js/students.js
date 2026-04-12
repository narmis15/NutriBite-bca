document.addEventListener("DOMContentLoaded", function () {
    const addCartButtons = document.querySelectorAll(".add-cart");

    addCartButtons.forEach(btn => {
        btn.addEventListener("click", function () {
            const foodId = this.dataset.id;
            const originalText = this.innerText;

            fetch('/Cart/AddToCart', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    productId: parseInt(foodId),
                    quantity: 1
                })
            })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    this.innerHTML = '<i class="bi bi-check2"></i> Added';
                    this.classList.add('bg-success');
                    
                    // Trigger cart count update if site.js has a global function
                    if (window.loadCartCount) {
                        window.loadCartCount();
                    }

                    setTimeout(() => {
                        this.innerText = originalText;
                        this.classList.remove('bg-success');
                    }, 2000);
                } else {
                    alert(data.message || "Failed to add item to cart.");
                    if (data.message === "Please login first") {
                        window.location.href = '/Auth/Login';
                    }
                }
            })
            .catch(error => {
                console.error('Error:', error);
                alert("An error occurred while adding to cart.");
            });
        });
    });
});
