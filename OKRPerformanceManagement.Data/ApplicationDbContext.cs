using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Models;

namespace OKRPerformanceManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<PerformanceReview> PerformanceReviews { get; set; }
        public DbSet<Objective> Objectives { get; set; }
        public DbSet<KeyResult> KeyResults { get; set; }
        public DbSet<ReviewComment> ReviewComments { get; set; }
        public DbSet<EmployeeRole> EmployeeRoles { get; set; }
        public DbSet<OKRTemplate> OKRTemplates { get; set; }
        public DbSet<OKRTemplateObjective> OKRTemplateObjectives { get; set; }
        public DbSet<OKRTemplateKeyResult> OKRTemplateKeyResults { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Manager)
                .WithMany(e => e.Subordinates)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PerformanceReview>()
                .HasOne(pr => pr.Employee)
                .WithMany(e => e.PerformanceReviews)
                .HasForeignKey(pr => pr.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PerformanceReview>()
                .HasOne(pr => pr.Manager)
                .WithMany(e => e.ManagedReviews)
                .HasForeignKey(pr => pr.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Objective>()
                .HasOne(o => o.PerformanceReview)
                .WithMany(pr => pr.Objectives)
                .HasForeignKey(o => o.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<KeyResult>()
                .HasOne(kr => kr.Objective)
                .WithMany(o => o.KeyResults)
                .HasForeignKey(kr => kr.ObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewComment>()
                .HasOne(rc => rc.PerformanceReview)
                .WithMany(pr => pr.Comments)
                .HasForeignKey(rc => rc.PerformanceReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewComment>()
                .HasOne(rc => rc.Commenter)
                .WithMany()
                .HasForeignKey(rc => rc.CommenterId)
                .OnDelete(DeleteBehavior.Restrict);

            // OKR Template relationships
            modelBuilder.Entity<OKRTemplateObjective>()
                .HasOne(o => o.OKRTemplate)
                .WithMany(t => t.Objectives)
                .HasForeignKey(o => o.OKRTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OKRTemplateKeyResult>()
                .HasOne(kr => kr.OKRTemplateObjective)
                .WithMany(o => o.KeyResults)
                .HasForeignKey(kr => kr.OKRTemplateObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);

            // Performance Review to OKR Template relationship
            modelBuilder.Entity<PerformanceReview>()
                .HasOne(pr => pr.OKRTemplate)
                .WithMany()
                .HasForeignKey(pr => pr.OKRTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            // Employee to ApplicationUser relationship
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.UserId)
                .IsUnique();

            // Employee to EmployeeRole relationship
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.RoleEntity)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // EmployeeRole to OKRTemplate relationship
            modelBuilder.Entity<OKRTemplate>()
                .HasOne(t => t.RoleEntity)
                .WithMany(r => r.OKRTemplates)
                .HasForeignKey(t => t.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
