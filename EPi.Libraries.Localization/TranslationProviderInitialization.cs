// Copyright© 2014 Jeroen Stemerdink. All Rights Reserved.
// 
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Specialized;
using System.Linq;

using EPi.Libraries.Localization.Models;

using EPiServer;
using EPiServer.Core;
using EPiServer.Events;
using EPiServer.Events.Clients;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;

using log4net;

namespace EPi.Libraries.Localization
{
    /// <summary>
    ///     The initialization module for the translation provider.
    /// </summary>
    [InitializableModule]
    [ModuleDependency(typeof(FrameworkInitialization))]
    public class TranslationProviderInitialization : IInitializableModule
    {
        #region Constants

        /// <summary>
        ///     The provider name.
        /// </summary>
        private const string ProviderName = "Translations";

        #endregion

        #region Static Fields

        /// <summary>
        ///     Initializes the <see cref="LogManager">LogManager</see> for the <see cref="TranslationProviderInitialization" />
        ///     class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(TranslationProviderInitialization));

        // Generate unique id for the raiser.
        private static readonly Guid TranslationProviderRaiserId = new Guid("cb4e20de-5dd3-48cd-b72a-0ecc3ce08cee");

        // Generate unique id for the reload event.
        private static readonly Guid TranslationsUpdatedEventId = new Guid("9674113d-5135-49ff-8d2b-80ee6ae8f9e9");

        /// <summary>
        ///     Check if the initialization has been done.
        /// </summary>
        private static bool initialized;

        #endregion

        #region Fields

        /// <summary>
        ///     The provider based localization service
        /// </summary>
        private ProviderBasedLocalizationService providerBasedLocalizationService;

        /// <summary>
        ///     The translation provider
        /// </summary>
        private TranslationProvider translationProvider;

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets the content events.
        /// </summary>
        /// <value>The content events.</value>
        protected Injected<IContentEvents> ContentEvents { get; set; }

        /// <summary>
        ///     Gets or sets the event service.
        /// </summary>
        /// <value>The event service.</value>
        protected Injected<IEventRegistry> EventService { get; set; }

        /// <summary>
        ///     Gets or sets the provider based localization service.
        /// </summary>
        /// <value>The provider based localization service.</value>
        protected Injected<LocalizationService> LocalizationService { get; set; }

        /// <summary>
        ///     Gets or sets the provider based localization service.
        /// </summary>
        /// <value>The provider based localization service.</value>
        private ProviderBasedLocalizationService ProviderBasedLocalizationService
        {
            get
            {
                return this.providerBasedLocalizationService
                       ?? (this.providerBasedLocalizationService =
                           this.LocalizationService.Service as ProviderBasedLocalizationService);
            }
        }

        /// <summary>
        ///     Gets or sets the localization provider.
        /// </summary>
        /// <value>The localization provider.</value>
        private TranslationProvider TranslationProvider
        {
            get
            {
                return this.translationProvider ?? (this.translationProvider = this.GetTranslationProvider());
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Initializes this instance.
        /// </summary>
        /// <param name="context">
        ///     The context.
        /// </param>
        /// <remarks>
        ///     Gets called as part of the EPiServer Framework initialization sequence. Note that it will be called
        ///     only once per AppDomain, unless the method throws an exception. If an exception is thrown, the initialization
        ///     method will be called repeatedly for each request reaching the site until the method succeeds.
        /// </remarks>
        public void Initialize(InitializationEngine context)
        {
            // If there is no context, we can't do anything.
            if (context == null)
            {
                return;
            }

            // If already initialized, no need to do it again.
            if (initialized)
            {
                return;
            }

            Log.Info("[Localization] Initializing translation provider.");

            // Initialize the provider after the initialization is complete, else the StartPage cannot be found.
            context.InitComplete += this.InitComplete;

            this.ContentEvents.Service.PublishedContent += this.InstancePublishedContent;
            this.ContentEvents.Service.MovedContent += this.InstanceChangedContent;
            this.ContentEvents.Service.DeletedContent += this.InstanceChangedContent;

            // Make sure the RemoteCacheSynchronization event is registered before the custome event.
            this.EventService.Service.Get(RemoteCacheSynchronization.RemoveFromCacheEventId);

            // Attach a custom event to update the translations when translations are updated, eg. in LoadBalanced environments.
            Event translationsUpdated = this.EventService.Service.Get(TranslationsUpdatedEventId);
            translationsUpdated.Raised += this.TranslationsUpdatedEventRaised;

            initialized = true;

            Log.Info("[Localization] Translation provider initialized.");
        }

        /// <summary>
        ///     Preloads the module.
        /// </summary>
        /// <param name="parameters">
        ///     The parameters.
        /// </param>
        /// <remarks>
        ///     This method is only available to be compatible with "AlwaysRunning" applications in .NET 4 / IIS 7.
        ///     It currently serves no purpose.
        /// </remarks>
        public void Preload(string[] parameters)
        {
        }

        /// <summary>
        ///     Resets the module into an uninitialized state.
        /// </summary>
        /// <param name="context">
        ///     The context.
        /// </param>
        /// <remarks>
        ///     <para>
        ///         This method is usually not called when running under a web application since the web app may be shut down very
        ///         abruptly, but your module should still implement it properly since it will make integration and unit testing
        ///         much simpler.
        ///     </para>
        ///     <para>
        ///         Any work done by
        ///         <see
        ///             cref="M:EPiServer.Framework.IInitializableModule.Initialize(EPiServer.Framework.Initialization.InitializationEngine)" />
        ///         as well as any code executing on
        ///         <see cref="E:EPiServer.Framework.Initialization.InitializationEngine.InitComplete" />
        ///         should be reversed.
        ///     </para>
        /// </remarks>
        public void Uninitialize(InitializationEngine context)
        {
            // If there is no context, we can't do anything.
            if (context == null)
            {
                return;
            }

            // If already uninitialized, no need to do it again.
            if (!initialized)
            {
                return;
            }

            Log.Info("[Localization] Uninitializing translation provider.");

            initialized = this.UnLoadProvider(this.TranslationProvider);

            Log.Info("[Localization] Translation provider uninitialized.");
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Run when initialization is complete.
        /// </summary>
        /// <param name="sender">
        ///     The sender.
        /// </param>
        /// <param name="e">
        ///     The <see cref="EventArgs" /> instance containing the event data.
        /// </param>
        internal void InitComplete(object sender, EventArgs e)
        {
            initialized = this.LoadProvider();
        }

        /// <summary>
        ///     Get the localization provider.
        /// </summary>
        /// <returns>
        ///     The <see cref="LocalizationProvider" />.
        /// </returns>
        private TranslationProvider GetTranslationProvider()
        {
            if (this.ProviderBasedLocalizationService == null)
            {
                return null;
            }

            // Gets any provider that has the same name as the one initialized.
            LocalizationProvider localizationProvider =
                this.providerBasedLocalizationService.Providers.FirstOrDefault(
                    p => p.Name.Equals(ProviderName, StringComparison.Ordinal));

            return localizationProvider as TranslationProvider;
        }

        /// <summary>
        ///     Content has changed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ContentEventArgs" /> instance containing the event data.</param>
        private void InstanceChangedContent(object sender, ContentEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (!(e.Content is TranslationContainer) && !(e.Content is TranslationItem)
                && !(e.Content is CategoryTranslationContainer))
            {
                return;
            }

            this.UpdateTranslations();

            this.RaiseEvent("[Localization] Translation updated.");
        }

        /// <summary>
        ///     Content was published event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ContentEventArgs" /> instance containing the event data.</param>
        private void InstancePublishedContent(object sender, ContentEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.Content.ContentLink == ContentReference.StartPage)
            {
                TranslationFactory.Instance.SetTranslationContainer();
            }

            if (!(e.Content is TranslationContainer) && !(e.Content is TranslationItem)
                && !(e.Content is CategoryTranslationContainer))
            {
                return;
            }

            if (TranslationFactory.Instance.TranslationServiceActivated)
            {
                TranslationFactory.Instance.TranslateThemAll(e.Content);
            }

            this.UpdateTranslations();

            this.RaiseEvent("[Localization] Translation updated.");
        }

        /// <summary>
        ///     Loads the provider.
        /// </summary>
        /// <returns>
        ///     [true] if the provider has been loaded.
        /// </returns>
        private bool LoadProvider()
        {
            if (this.ProviderBasedLocalizationService == null)
            {
                return false;
            }

            // This config value could tell the provider where to find the translations, 
            // set to 0 though, will be looked up after initialization in the provider itself.
            NameValueCollection configValues = new NameValueCollection { { "containerid", "0" } };

            TranslationProvider temporaryTranslationProvider = null;
            TranslationProvider localizationProvider;

            try
            {
                temporaryTranslationProvider = new TranslationProvider();
                temporaryTranslationProvider.Initialize(ProviderName, configValues);
                localizationProvider = temporaryTranslationProvider;
                temporaryTranslationProvider = null;
            }
            finally
            {
                if (temporaryTranslationProvider != null)
                {
                    temporaryTranslationProvider.Dispose();
                }
            }

            // Add it at the end of the list of providers.
            try
            {
                this.ProviderBasedLocalizationService.Providers.Add(localizationProvider);
            }
            catch (NotSupportedException notSupportedException)
            {
                Log.Error("[Localization] Error add provider to the Localization Service.", notSupportedException);
                return false;
            }

            return true;
        }

        private void RaiseEvent(string message)
        {
            // Raise the TranslationsUpdated event.
            this.EventService.Service.Get(TranslationsUpdatedEventId).Raise(TranslationProviderRaiserId, message);
        }

        /// <summary>
        ///     Removes from cache event raised.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventNotificationEventArgs" /> instance containing the event data.</param>
        private void TranslationsUpdatedEventRaised(object sender, EventNotificationEventArgs e)
        {
            // We don't want to process events raised on this machine so we will check the raiser id.
            if (e.RaiserId == TranslationProviderRaiserId)
            {
                return;
            }

            this.UpdateTranslations();

            Log.Info("[Localization] Translations updated on other machine. Reloaded provider.");
        }

        /// <summary>
        ///     Unload provider.
        /// </summary>
        /// <returns>
        ///     [false] if the provider has been unloaded, as it's not initialized anymore.
        /// </returns>
        private bool UnLoadProvider(LocalizationProvider localizationProvider)
        {
            if (this.ProviderBasedLocalizationService == null)
            {
                return false;
            }

            if (this.TranslationProvider == null)
            {
                return false;
            }

            // If found, remove it.
            this.ProviderBasedLocalizationService.Providers.Remove(localizationProvider);

            return false;
        }

        /// <summary>
        ///     Update the translations.
        /// </summary>
        private void UpdateTranslations()
        {
            if (this.TranslationProvider == null)
            {
                Log.Info("[Localization] No translation provider found. Translations were not updated.");
                return;
            }

            this.TranslationProvider.UpdateTranslations();
        }

        #endregion
    }
}