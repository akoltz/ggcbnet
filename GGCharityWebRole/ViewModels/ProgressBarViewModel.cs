using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole.ViewModels
{
    public class ProgressBarViewModel
    {
        /// <summary>
        /// The total possible value.
        /// </summary>
        public int Total;

        /// <summary>
        /// The amount of progress to fill.
        /// </summary>
        public int Progress;
        
        /// <summary>
        /// The text to display inside the progress bar.
        /// </summary>
        public string Text;

        /// <summary>
        ///  The class for the progress bar container.  Is added in addition
        ///  to the 'progress' class
        /// </summary>
        public string ProgressClass;

        /// <summary>
        /// The class for the progress bar itself (the part that fills up
        /// based on the value of Progress).  Is added in addition to the
        /// 'progress-bar' class.
        /// </summary>
        public string ProgressBarClass;
    }
}