using GGCharityData;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.WindowsAzure;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace GGCharityWebRole.Workarounds.DoNotUse
{
    /// <summary>
    /// This class is a workaround for a bug in vs2013 where it cannot scaffold a controller
    /// for the raw data context.  The problem is related to the raw data context's constructor.
    /// We work around it by using a default constructor here.  This context should not truly be used
    /// in product code.
    /// </summary>
    public class ScaffoldingDbContext : IdentityDbContext<GGCharityUser>
    {
        public ScaffoldingDbContext()
        {
        }

        //public virtual DbSet<ContentReport> ReportedContent { get; set; }
        public virtual DbSet<Charity> Charities { get; set; }
        public virtual DbSet<Pledge> Pledges { get; set; } 
        //public virtual DbSet<Announcement> Announcements { get; set; }
        public virtual DbSet<EventRegistration> EventRegistrations { get; set; }
        public virtual DbSet<Event> Events { get; set; }
        public virtual DbSet<Game> Games { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EventRegistration>()
                .HasRequired(r => r.Event)
                .WithMany(e => e.Registrations)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<EventRegistration>()
                .HasRequired(r => r.User)
                .WithMany(u => u.EventRegistrations)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Pledge>()
                .HasRequired(p => p.Receiver)
                .WithMany(r => r.PledgesReceived)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Pledge>()
                 .HasRequired(p => p.Donor)
                 .WithMany(r => r.Pledges)
                 .WillCascadeOnDelete(false);

            base.OnModelCreating(modelBuilder);
        }

    }
}