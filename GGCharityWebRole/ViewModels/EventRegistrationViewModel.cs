using GGCharityData;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace GGCharityWebRole.ViewModels
{
    public class EventRegistrationViewModel
    {
        public EventRegistrationViewModel()
        {
        }

        public EventRegistrationViewModel(EventRegistration registration)
        {
            WinsGoal = registration.WinsGoal;
            EventId = registration.EventId;
        }

        [Required]
        [Display(Name="Wins Goal")]
        public int WinsGoal { get; set; }

        [Required]
        public int EventId { get; set; }
    }
}