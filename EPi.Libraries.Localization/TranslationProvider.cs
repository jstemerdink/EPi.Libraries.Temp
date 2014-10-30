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

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;

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
    public class TranslationProvider : MemoryLocalizationProvider
    {
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
        internal void LoadTranslations()
        {
            foreach (CultureInfo cultureInfo in this.AvailableLanguages)
            {
                this.AddKey(TranslationFactory.Instance.TranslationContainerReference, cultureInfo);
            }
        }

        internal void UpdateTranslations()
        {
            this.ClearStrings();
            this.LoadTranslations();
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