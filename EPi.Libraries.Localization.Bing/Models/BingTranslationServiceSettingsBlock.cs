using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;

namespace EPi.Libraries.Localization.Bing.Models
{
    /// <summary>
    /// Class BingServiceSettingsBlock.
    /// </summary>
    [ContentType(DisplayName = "Bing translation service settings", GUID = "87a11dce-e2ab-439d-aea5-12f16d76c41d",
        Description = "Settings for the Bing translation service", AvailableInEditMode = false)]
    public class BingTranslationServiceSettingsBlock : BlockData
    {
        #region Public Properties

        /// <summary>
        ///     Gets or sets the alchemy key.
        /// </summary>
        /// <value>The alchemy key.</value>
        [CultureSpecific(false)]
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Client ID", Description = "The client id for the Bing translation service", GroupName = SystemTabNames.Settings,
            Order = 1)]
        public virtual string ClientID { get; set; }

        
        [CultureSpecific(false)]
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Client Secret", Description = "The client secret for the Bing translation service.",
            GroupName = SystemTabNames.Settings, Order = 2)]
        public virtual string ClientSecret { get; set; }

        #endregion
    }
}


