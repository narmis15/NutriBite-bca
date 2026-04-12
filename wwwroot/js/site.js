// Global "Add to Cart" functionality for all items
$(document).on("click", ".add-to-cart-btn", function () {
    const foodId = $(this).data("id");
    
    console.log("Adding item to cart, ID:", foodId);

    if (!foodId) {
        console.error("No product ID found for this button.");
        return;
    }

    const formData = new URLSearchParams();
    formData.append("productId", foodId);
    formData.append("quantity", 1);

    fetch("/Cart/AddToCart", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded"
        },
        body: formData
    })
    .then(response => response.json())
    .then(data => {
        console.log("Server response:", data);
        if (data.success) {
            alert("Item added to cart");
            // Update cart count UI if needed
            if (typeof loadCartCount === 'function') {
                loadCartCount();
            }
        } else {
            alert(data.message || "Login required to add items to cart.");
            if (!data.success && !data.message) {
                window.location.href = "/Auth/Login";
            }
        }
    })
    .catch(error => {
        console.error("Error adding item to cart:", error);
    });
});
