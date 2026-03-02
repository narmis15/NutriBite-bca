using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NUTRIBITE.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AddCategory> AddCategories { get; set; }

    public virtual DbSet<AddProduct> AddProducts { get; set; }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<Carttable> Carttables { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<DailyCalorieEntry> DailyCalorieEntries { get; set; }

    public virtual DbSet<Food> Foods { get; set; }

    public virtual DbSet<HealthSurvey> HealthSurveys { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Offer> Offers { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderTable> OrderTables { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PickupSlot> PickupSlots { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<ReviewUser> ReviewUsers { get; set; }

    public virtual DbSet<SlotBlock> SlotBlocks { get; set; }

    public virtual DbSet<Stock> Stocks { get; set; }

    public virtual DbSet<UserSignup> UserSignups { get; set; }

    public virtual DbSet<VendorSignup> VendorSignups { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=SIMRAN\\SQLEXPRESS;Database=FoodDeliveryDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AddCategory>(entity =>
        {
            entity.HasKey(e => e.Cid).HasName("PK__AddCateg__D837D05F04218349");

            entity.ToTable("AddCategory");

            entity.Property(e => e.Cid).HasColumnName("cid");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.ImagePath)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.MealCategory)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MealPic)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ProductCategory).IsUnicode(false);
            entity.Property(e => e.ProductPic).IsUnicode(false);
        });

        modelBuilder.Entity<AddProduct>(entity =>
        {
            entity.HasKey(e => e.Pid);

            entity.ToTable("AddProduct");

            entity.Property(e => e.Pid).ValueGeneratedNever();
            entity.Property(e => e.Description).IsUnicode(false);
            entity.Property(e => e.Dper).HasColumnName("dper");
            entity.Property(e => e.Pic).IsUnicode(false);
            entity.Property(e => e.Pname)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("PName");
            entity.Property(e => e.Pprice).HasColumnName("PPrice");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Vid).HasColumnName("VId");
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Admin__3214EC07A8BE545E");

            entity.ToTable("Admin");

            entity.Property(e => e.Password)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Carttable>(entity =>
        {
            entity.HasKey(e => e.Crid);

            entity.ToTable("Carttable");

            entity.Property(e => e.Crid)
                .ValueGeneratedNever()
                .HasColumnName("CRid");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.ToTable("Coupon");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.Startdate).HasColumnType("datetime");
            entity.Property(e => e.Validtill).HasColumnType("datetime");
        });

        modelBuilder.Entity<DailyCalorieEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DailyCal__3214EC0736203BD9");

            entity.ToTable("DailyCalorieEntry");

            entity.Property(e => e.Carbs).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.Date).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.Fats).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.FoodName).HasMaxLength(256);
            entity.Property(e => e.Protein).HasColumnType("decimal(6, 2)");
        });

        modelBuilder.Entity<Food>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Foods__3214EC077930ACD6");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ImagePath).HasMaxLength(300);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.PreparationTime).HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Rating).HasDefaultValue(0.0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");
        });

        modelBuilder.Entity<HealthSurvey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HealthSu__3214EC0770106C14");

            entity.Property(e => e.ActivityLevel).HasMaxLength(64);
            entity.Property(e => e.Bmi)
                .HasColumnType("decimal(6, 2)")
                .HasColumnName("BMI");
            entity.Property(e => e.Bmr)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("BMR");
            entity.Property(e => e.ChronicDiseases).HasMaxLength(1000);
            entity.Property(e => e.DietaryPreference).HasMaxLength(64);
            entity.Property(e => e.FoodAllergies).HasMaxLength(1000);
            entity.Property(e => e.Gender).HasMaxLength(32);
            entity.Property(e => e.Goal).HasMaxLength(32);
            entity.Property(e => e.HeightCm).HasColumnType("decimal(6, 2)");
            entity.Property(e => e.WeightKg).HasColumnType("decimal(6, 2)");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("Location");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.City)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Region)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.State)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");

            entity.Property(e => e.NotificationId)
                .ValueGeneratedNever()
                .HasColumnName("NotificationID");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Message)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ReceiverId).HasColumnName("ReceiverID");
            entity.Property(e => e.ReceiverType)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Offer>(entity =>
        {
            entity.ToTable("offers");

            entity.Property(e => e.Offerid)
                .ValueGeneratedNever()
                .HasColumnName("offerid");
            entity.Property(e => e.Dprice).HasColumnName("dprice");
            entity.Property(e => e.PId).HasColumnName("pId");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.VId).HasColumnName("vId");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OrderIte__3214EC076F89A167");

            entity.HasIndex(e => e.OrderId, "IX_OrderItems_OrderId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Instructions).HasMaxLength(1000);
            entity.Property(e => e.ItemName).HasMaxLength(300);
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_OrderTable");
        });

        modelBuilder.Entity<OrderTable>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__OrderTab__C3905BCF5037D383");

            entity.ToTable("OrderTable");

            entity.HasIndex(e => e.PickupSlot, "IX_OrderTable_PickupSlot");

            entity.HasIndex(e => e.Status, "IX_OrderTable_Status");

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelledBy).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.CustomerPhone).HasMaxLength(50);
            entity.Property(e => e.FlagReason).HasMaxLength(500);
            entity.Property(e => e.IsFlagged).HasDefaultValue(false);
            entity.Property(e => e.IsResolved).HasDefaultValue(false);
            entity.Property(e => e.PaymentStatus).HasMaxLength(100);
            entity.Property(e => e.PickupSlot).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.OrderTables)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_OrderTable_UserSignup");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payment__3214EC07BD90E8E7");

            entity.ToTable("Payment");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsRefunded).HasDefaultValue(false);
            entity.Property(e => e.PaymentMode).HasMaxLength(100);
            entity.Property(e => e.RefundMethod).HasMaxLength(200);
            entity.Property(e => e.RefundStatus).HasMaxLength(200);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_Payment_OrderTable");
        });

        modelBuilder.Entity<PickupSlot>(entity =>
        {
            entity.HasKey(e => e.SlotId).HasName("PK__PickupSl__0A124AAF74511C4F");

            entity.Property(e => e.Capacity).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsDisabled).HasDefaultValue(false);
            entity.Property(e => e.SlotLabel).HasMaxLength(200);
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.Rid);

            entity.ToTable("Rating");

            entity.Property(e => e.Rid).ValueGeneratedNever();
            entity.Property(e => e.Date)
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Message).IsUnicode(false);
        });

        modelBuilder.Entity<ReviewUser>(entity =>
        {
            entity.ToTable("ReviewUser");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Message)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Subject)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SlotBlock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SlotBloc__3214EC07C70178B4");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Slot).WithMany(p => p.SlotBlocks)
                .HasForeignKey(d => d.SlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SlotBlocks_PickupSlots");
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("stock");

            entity.Property(e => e.StockId)
                .ValueGeneratedNever()
                .HasColumnName("StockID");
            entity.Property(e => e.LastUpdatedDate).HasColumnType("datetime");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.VendorId).HasColumnName("VendorID");
        });

        modelBuilder.Entity<UserSignup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserSign__3214EC075B80A025");

            entity.ToTable("UserSignup");

            entity.HasIndex(e => e.Email, "UQ__UserSign__A9D10534529A670A").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(100);
        });

        modelBuilder.Entity<VendorSignup>(entity =>
        {
            entity.HasKey(e => e.VendorId).HasName("PK__VendorSi__FC8618F3C9712783");

            entity.ToTable("VendorSignup");

            entity.HasIndex(e => e.Email, "UQ__VendorSi__A9D105341CC5F169").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.VendorName).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
