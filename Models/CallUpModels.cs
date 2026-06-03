using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CallUp.Models
{
    public class ApplicationUser : IdentityUser<int, ApplicationUserLogin, ApplicationUserRole, ApplicationUserClaim>
    {
        public string FullName { get; set; }
        public string UserRole { get; set; }
        public string About { get; set; }
        public string IdNumber { get; set; }
        public string CompanyName { get; set; }
        public string CompanyRegNo { get; set; }
        public string Category { get; set; }
        public bool IsVerified { get; set; }
        public bool IsActive { get; set; } = true;
        public string StatusReason { get; set; }
        public decimal EscrowWallet { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string BranchCode { get; set; }
        public bool IsInstantPayment { get; set; }
        public string Location { get; set; }
        public string ProfileImagePath { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser, int> manager)
        {
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            return userIdentity;
        }
    }

    // Required for Custom Primary Key types in Identity
    public class ApplicationUserLogin : IdentityUserLogin<int> { }
    public class ApplicationUserClaim : IdentityUserClaim<int> { }
    public class ApplicationUserRole : IdentityUserRole<int> { }
    public class ApplicationRole : IdentityRole<int, ApplicationUserRole>
    {
        public ApplicationRole() { }
        public ApplicationRole(string name) { Name = name; }
    }

    public class Address
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Label { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string PostalCode { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsDefault { get; set; }
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public virtual ICollection<ServiceRequest> ServiceRequests { get; set; }
    }

    public class ServiceRequest
    {
        [Key]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int CategoryId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string SpecialNotes { get; set; }
        public string CompletionNotes { get; set; }
        public string PriceRange { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Status { get; set; } = "Moderation";
        public int ViewCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ServiceDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? WorkOrderDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? SelectedProviderId { get; set; }

        public virtual ApplicationUser Customer { get; set; }
        public virtual Category Category { get; set; }
        public virtual ICollection<Bid> Bids { get; set; }
        public virtual ICollection<RequestImage> RequestImages { get; set; }

        // Unmapped UI Helper Properties
        [NotMapped]
        public string CategoryName { get; set; }
        [NotMapped]
        public int BidCount { get; set; }
        [NotMapped]
        public List<string> ImagePaths { get; set; } = new List<string>();
        [NotMapped]
        public string CustomerName { get; set; }
        [NotMapped]
        public string CustomerPhone { get; set; }
        [NotMapped]
        public string CustomerEmail { get; set; }
        [NotMapped]
        public decimal Amount { get; set; }
        [NotMapped]
        public string ETA { get; set; }
        [NotMapped]
        public decimal? LowestBid { get; set; }
        [NotMapped]
        public decimal? MyBidAmount { get; set; }
        [NotMapped]
        public string MyBidEta { get; set; }
        [NotMapped]
        public double? DistanceInKm { get; set; }
        [NotMapped]
        public Quote SelectedQuote { get; set; }
    }

    public class Bid
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int ProviderId { get; set; }
        public decimal Amount { get; set; }
        public string ETA { get; set; }
        public string Notes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ServiceRequest Request { get; set; }
        public virtual ApplicationUser Provider { get; set; }
    }

    public class ShieldPayment
    {
        public int Id { get; set; }
        public int BidId { get; set; }
        public decimal Amount { get; set; }
        public bool PenaltyShield { get; set; }
        public string Status { get; set; } = "Held";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Bid Bid { get; set; }
    }

    public class ProviderDocument
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DocumentType { get; set; }
        public string FilePath { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public virtual ApplicationUser User { get; set; }
    }

    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? RequestId { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ApplicationUser User { get; set; }
    }

    public class CompletionImage
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string ImagePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ServiceRequest Request { get; set; }
    }

    public class RequestImage
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string ImagePath { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public virtual ServiceRequest Request { get; set; }
    }

    public class Rating
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int FromCustomerId { get; set; }
        public int ToProviderId { get; set; }
        public int Score { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ServiceRequest Request { get; set; }
        public virtual ApplicationUser FromCustomer { get; set; }
        public virtual ApplicationUser ToProvider { get; set; }
    }

    public class ActivityLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public string IPAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ApplicationUser User { get; set; }
    }

    public class Quote
    {
        public int Id { get; set; }
        public int BidId { get; set; }
        public string FilePath { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Bid Bid { get; set; }
    }

    public class Message
    {
        [Key]
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("SenderId")]
        public virtual ApplicationUser Sender { get; set; }
        [ForeignKey("ReceiverId")]
        public virtual ApplicationUser Receiver { get; set; }
    }

    public class CallUpContext : IdentityDbContext<ApplicationUser, ApplicationRole, int, ApplicationUserLogin, ApplicationUserRole, ApplicationUserClaim>
    {
        public CallUpContext() : base(CallUp.Helpers.DbConfig.GetConnectionStringName())
        {
            Database.SetInitializer<CallUpContext>(null);
            Configuration.LazyLoadingEnabled = true;
        }

        public static CallUpContext Create()
        {
            return new CallUpContext();
        }

        public DbSet<Address> Addresses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<Bid> Bids { get; set; }
        public DbSet<ShieldPayment> ShieldPayments { get; set; }
        public DbSet<ProviderDocument> ProviderDocuments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CompletionImage> CompletionImages { get; set; }
        public DbSet<RequestImage> RequestImages { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<Message> Messages { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("Users");
            modelBuilder.Entity<ApplicationRole>().ToTable("Roles");
            modelBuilder.Entity<ApplicationUserRole>().ToTable("UserRoles");
            modelBuilder.Entity<ApplicationUserLogin>().ToTable("UserLogins");
            modelBuilder.Entity<ApplicationUserClaim>().ToTable("UserClaims");

            // Define custom relationships if necessary
        }
    }
}
