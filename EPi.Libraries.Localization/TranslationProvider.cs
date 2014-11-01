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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;

using EPi.Libraries.Localization.Models;

using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;

namespace EPi.Libraries.Localization
{
    /// <summary>
    ///     The translation provider.
    /// </summary>
    public class TranslationProvider : MemoryLocalizationProvider, IDisposable
    {
        #region Fields

        /// <summary>
        /// The cache lock
        /// </summary>
        private readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Indicate whether the provider has been disposed.
        /// </summary>
        private bool isDisposed;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Finalizes an instance of the <see cref="TranslationProvider" /> class.
        /// </summary>
        ~TranslationProvider()
        {
            this.Dispose(false);
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets all available languages from the translation container.
        ///     An available language does not need to contain any translations.
        /// </summary>
        public override IEnumerable<CultureInfo> AvailableLanguages
        {
            get
            {
                return this.LanguageBranchRepository.Service.ListEnabled().Select(p => p.Culture);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets the content repository.
        /// </summary>
        /// <value>The content repository.</value>
        protected Injected<IContentRepository> ContentRepository { get; set; }

        /// <summary>
        ///     Gets or sets the language branch repository.
        /// </summary>
        /// <value>The language branch repository.</value>
        protected Injected<ILanguageBranchRepository> LanguageBranchRepository { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Gets all localized strings for the specified culture below the specified key.
        /// </summary>
        /// <param name="originalKey">The original key that was passed into any GetString method.</param>
        /// <param name="normalizedKey">The <paramref name="originalKey" /> normalized and split into an array</param>
        /// <param name="culture">The requested culture for the localized strings.</param>
        /// <returns>All localized strings below the specified key.</returns>
        /// <seealso
        ///     cref="M:EPiServer.Framework.Localization.LocalizationService.GetStringByCulture(System.String,System.Globalization.CultureInfo)" />
        public override IEnumerable<ResourceItem> GetAllStrings(
            string originalKey,
            string[] normalizedKey,
            CultureInfo culture)
        {
            IEnumerable<ResourceItem> allStrings;

            this.cacheLock.EnterReadLock();

            try
            {
                allStrings = base.GetAllStrings(originalKey, normalizedKey, culture);
            }
            finally
            {
                this.cacheLock.ExitReadLock();
            }

            return allStrings;
        }

        /// <summary>
        ///     Gets the localized string for the specified key in the specified culture.
        /// </summary>
        /// <param name="originalKey">The original key that was passed into any GetString method.</param>
        /// <param name="normalizedKey">The <paramref name="originalKey" /> normalized and split into an array</param>
        /// <param name="culture">The requested culture for the localized string.</param>
        /// <returns>A localized string or <c>null</c> if no resource is found for the given key and culture.</returns>
        /// <seealso
        ///     cref="M:EPiServer.Framework.Localization.LocalizationService.GetStringByCulture(System.String,System.Globalization.CultureInfo)" />
        public override string GetString(string originalKey, string[] normalizedKey, CultureInfo culture)
        {
            string translation;

            this.cacheLock.EnterReadLock();

            try
            {
                translation = base.GetString(originalKey, normalizedKey, culture);
            }
            finally
            {
                this.cacheLock.ExitReadLock();
            }

            return translation;
        }

        /// <summary>
        ///     Initializes the provider.
        /// </summary>
        /// <param name="name">
        ///     The friendly name of the provider.
        /// </param>
        /// <param name="config">
        ///     A collection of the name/value pairs representing the provider-specific attributes specified in the configuration
        ///     for this provider.
        /// </param>
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            this.LoadTranslations();
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Load the translations.
        /// </summary>
        private void LoadTranslations()
        {
            this.cacheLock.EnterWriteLock();

            try
            {
                foreach (CultureInfo cultureInfo in this.AvailableLanguages)
                {
                    this.AddKey(TranslationFactory.Instance.TranslationContainerReference, cultureInfo);
                }
            }
            finally
            {
                this.cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the translations.
        /// </summary>
        public void UpdateTranslations()
        {
            this.cacheLock.EnterWriteLock();

            try
            {
                this.ClearStrings();
                this.LoadTranslations();
            }
            finally
            {
                this.cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Disposes the specified disposing.
        /// </summary>
        /// <param name="disposing">The disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.cacheLock != null)
            {
                this.cacheLock.Dispose();
            }

            this.isDisposed = true;
        }

        /// <summary>
        ///     Adds the key.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="cultureInfo">The culture information.</param>
        private void AddKey(ContentReference container, CultureInfo cultureInfo)
        {
            if (ContentReference.IsNullOrEmpty(container))
            {
                return;
            }

            List<PageData> children =
                this.ContentRepository.Service.GetChildren<PageData>(container, new LanguageSelector(cultureInfo.Name))
                    .ToList();

            foreach (PageData child in children)
            {
                TranslationContainer translationContainer = child as TranslationContainer;

                if (translationContainer != null)
                {
                    this.AddKey(child.PageLink, cultureInfo);
                }

                CategoryTranslationContainer categoryTranslationContainer = child as CategoryTranslationContainer;

                if (categoryTranslationContainer != null)
                {
                    this.AddKey(child.PageLink, cultureInfo);
                }

                TranslationItem translationItem = child as TranslationItem;

                if (translationItem != null)
                {
                    this.AddString(cultureInfo, translationItem.LookupKey, translationItem.Translation);
                }
            }
        }

        #endregion
    }
}