SET QUOTED_IDENTIFIER ON;
GO

INSERT INTO Payments (UserId, OrderId, PaymentId, Amount, Currency, Status, CreatedAt, RazorpayOrderId, PaymentMode, IsRefunded, RefundStatus) VALUES (6, 266, 'pay_test_3', 300, 'INR', 'Success', '2026-03-02 12:30:00', 'order_test_3', 'UPI', 0, 'None');
INSERT INTO Payments (UserId, OrderId, PaymentId, Amount, Currency, Status, CreatedAt, RazorpayOrderId, PaymentMode, IsRefunded, RefundStatus) VALUES (420, 267, 'pay_test_4', 450, 'INR', 'Success', '2026-03-05 14:15:00', 'order_test_4', 'Card', 0, 'None');
INSERT INTO Payments (UserId, OrderId, PaymentId, Amount, Currency, Status, CreatedAt, RazorpayOrderId, PaymentMode, IsRefunded, RefundStatus) VALUES (430, 268, 'pay_test_5', 600, 'INR', 'Success', '2026-03-08 19:40:00', 'order_test_5', 'NetBanking', 0, 'None');
INSERT INTO Payments (UserId, OrderId, PaymentId, Amount, Currency, Status, CreatedAt, RazorpayOrderId, PaymentMode, IsRefunded, RefundStatus) VALUES (500, 269, 'pay_test_6', 800, 'INR', 'Success', '2026-03-11 11:20:00', 'order_test_6', 'Wallet', 0, 'None');
INSERT INTO Payments (UserId, OrderId, PaymentId, Amount, Currency, Status, CreatedAt, RazorpayOrderId, PaymentMode, IsRefunded, RefundStatus) VALUES (510, 270, 'pay_test_7', 250, 'INR', 'Success', '2026-03-14 20:10:00', 'order_test_7', 'UPI', 0, 'None');
GO
