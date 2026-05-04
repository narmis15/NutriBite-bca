import random
from datetime import timedelta, datetime

sql_script = []

# Mock 20 orders for March 2026
start_date = datetime(2026, 3, 1)

users = [6, 420, 430, 500, 510]
vendors = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

# VendorPayout updates to 0
sql_script.append('UPDATE VendorPayouts SET Status = 0 WHERE Status = 1 AND Id IN (7,8,9);')

for i in range(25):
    dt = start_date + timedelta(days=random.randint(0, 30), hours=random.randint(10, 20))
    dt_str = dt.strftime('%Y-%m-%d %H:%M:%S')
    user_id = random.choice(users)
    vendor_id = random.choice(vendors)
    amount = random.randint(200, 1000)
    vendor_amount = amount * 0.9
    comm_amount = amount * 0.1
    
    sql = f"""
    INSERT INTO OrderTable (UserId, CreatedAt, UpdatedAt, Status, PaymentStatus, TotalAmount, VendorAmount, CommissionAmount, VendorId, DeliveryCharge, GST)
    VALUES ({user_id}, '{dt_str}', '{dt_str}', 'Delivered', 'Paid', {amount}, {vendor_amount}, {comm_amount}, {vendor_id}, 40, {amount*0.05});
    """
    sql_script.append(sql)

# Mock Subscriptions for March
for i in range(5):
    dt = start_date + timedelta(days=random.randint(0, 30))
    dt_str = dt.strftime('%Y-%m-%d %H:%M:%S')
    end_date = dt + timedelta(days=30)
    end_dt_str = end_date.strftime('%Y-%m-%d %H:%M:%S')
    user_id = random.choice(users)
    amount = random.choice([1500, 3000])
    
    sql = f"""
    INSERT INTO Subscriptions (UserId, PlanName, StartDate, EndDate, Status, TotalAmount, CreatedAt, UpdatedAt)
    VALUES ({user_id}, 'Monthly Fit', '{dt_str}', '{end_dt_str}', 'Active', {amount}, '{dt_str}', '{dt_str}');
    """
    sql_script.append(sql)

with open('mock_data.sql', 'w') as f:
    f.write('\n'.join(sql_script))
