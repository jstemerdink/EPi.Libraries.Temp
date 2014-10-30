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
using System.Linq;
using System.Reflection;

using EPi.Libraries.Localization.DataAnnotations;
using EPi.Libraries.Localization.Models;

using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;
using EPiServer.ServiceLocation;

using log4net;

namespace EPi.Libraries.Localization
{
    /// <summary>
    ///     The TranslationFactory class, used for translation queries.
    /// </summary>
    public sealed class TranslationFactory
    {
        #region Static Fields

        /// <summary>
        ///     The synclock object.
        /// </summary>
        private static readonly object InstanceLock = new object();

        /// <summary>
        ///     Initializes the <see cref="LogManager">LogManager</see> for the <see cref="TranslationFactory" /> class.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TranslationFactory));

        /// <summary>
        ///     The one and only TranslationFactory instance.
        /// </summary>
        private static volatile TranslationFactory instance;

        #endregion

        #region Fields

        /// <summary>
        ///     The translation service
        /// </summary>
        private ITranslationService translationService;

        /// <summary>
        ///     Gets a value indicating whether [a translation service is activated].
        /// </summary>
        private bool? translationServiceActivated;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Prevents a default instance of the <see cref="TranslationFactory" /> class from being created.
        /// </summary>
        private TranslationFactory()
        {
            this.TranslationContainerReference = this.GetTranslationContainer();
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the instance of the TranslationFactory object.
        /// </summary>
        public static TranslationFactory Instance
        {
            get
            {
                // Double checked locking
                if (instance != null)
                {
                    return instance;
                }

                lock (InstanceLock)
                {
                    if (instance == null)
                    {
                        instance = new TranslationFactory();
                    }
                }

                return instance;
            }
        }

        /// <summary>
        ///     Gets or sets the content repository.
        /// </summary>
        /// <value>The content repository.</value>
        public Injected<IContentRepository> ContentRepository { get; set; }

        /// <summary>
        ///     Gets or sets the language branch repository.
        /// </summary>
        /// <value>The language branch repository.</value>
        public Injected<ILanguageBranchRepository> LanguageBranchRepository { get; set; }

        /// <summary>
        ///     Gets the reference to the translation container.
        /// </summary>
        public ContentReference TranslationContainerReference { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether [a translation service is activated].
        /// </summary>
        /// <value><c>true</c> if [translation service activated]; otherwise, <c>false</c>.</value>
        internal bool TranslationServiceActivated
        {
            get
            {
                return this.translationServiceActivated
                       ?? (this.translationServiceActivated = this.TranslationService != null).Value;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets the translation service.
        /// </summary>
        /// <value>The translation service.</value>
        private ITranslationService TranslationService
        {
            get
            {
                try
                {
                    return this.translationService
                           ?? (this.translationService = ServiceLocator.Current.GetInstance<ITranslationService>());
                }
                catch (ActivationException activationException)
                {
                    Logger.Info("[Localization] No translation service available", activationException);
                }

                return null;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Translates them all.
        /// </summary>
        /// <param name="content">The content.</param>
        internal void TranslateThemAll(IContent content)
        {
            if (!this.TranslationServiceActivated)
            {
                return;
            }

            PageData page = content as PageData;

            if (page == null)
            {
                return;
            }

            List<LanguageBranch> enabledLanguages = this.LanguageBranchRepository.Service.ListEnabled().ToList();

            foreach (LanguageBranch languageBranch in
                enabledLanguages.Where(lb => lb.Culture.Name != page.LanguageBranch))
            {
                this.CreateLanguageBranch(page, languageBranch.Culture.Name);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Sets the translation container.
        /// </summary>
        public void SetTranslationContainer()
        {
            this.TranslationContainerReference = this.GetTranslationContainer();
        }

        /// <summary>
        ///     Gets the name of the translation container property.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>System.Reflection.PropertyInfo</returns>
        private static PropertyInfo GetTranslationContainerProperty(ContentData page)
        {
            PropertyInfo translationContainerProperty =
                page.GetType().GetProperties().Where(HasAttribute<TranslationContainerAttribute>).FirstOrDefault();

            return translationContainerProperty;
        }

        /// <summary>
        ///     Determines whether the specified self has attribute.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyInfo">The propertyInfo.</param>
        /// <returns><c>true</c> if the specified self has attribute; otherwise, <c>false</c>.</returns>
        private static bool HasAttribute<T>(PropertyInfo propertyInfo) where T : Attribute
        {
            T attr = (T)Attribute.GetCustomAttribute(propertyInfo, typeof(T));

            return attr != null;
        }

        private void CreateLanguageBranch(PageData page, string languageBranch)
        {
            // Check if language already exists
            bool languageExists =
                this.ContentRepository.Service.GetLanguageBranches<PageData>(page.PageLink)
                    .Any(p => string.Compare(p.LanguageBranch, languageBranch, StringComparison.OrdinalIgnoreCase) == 0);

            if (languageExists)
            {
                return;
            }

            TranslationItem translationItem = page as TranslationItem;

            if (translationItem != null)
            {
                TranslationItem languageItemVersion =
                    this.ContentRepository.Service.CreateLanguageBranch<TranslationItem>(
                        page.PageLink,
                        new LanguageSelector(languageBranch));

                languageItemVersion.PageName = page.PageName;
                languageItemVersion.URLSegment = page.URLSegment;

                string translatedText = this.TranslationService.Translate(
                    translationItem.OriginalText,
                    page.LanguageID.Split(new char['-'])[0],
                    languageItemVersion.LanguageID.Split(new char['-'])[0]);

                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    return;
                }

                languageItemVersion.Translation = translatedText;

                if (!string.IsNullOrWhiteSpace(languageItemVersion.Translation))
                {
                    this.ContentRepository.Service.Save(languageItemVersion, SaveAction.Publish, AccessLevel.NoAccess);
                }
            }
            else
            {
                PageData languageVersion = this.ContentRepository.Service.CreateLanguageBranch<PageData>(
                    page.PageLink,
                    new LanguageSelector(languageBranch));

                languageVersion.PageName = page.PageName;
                languageVersion.URLSegment = page.URLSegment;

                this.ContentRepository.Service.Save(languageVersion, SaveAction.Publish, AccessLevel.NoAccess);
            }
        }

        /// <summary>
        ///     Get the translation container.
        /// </summary>
        /// <returns>
        ///     The <see cref="PageReference" /> to the translation container.
        /// </returns>
        private ContentReference GetTranslationContainer()
        {
            if (PageReference.IsNullOrEmpty(ContentReference.StartPage))
            {
                return PageReference.EmptyReference;
            }

            ContentData startPageData = this.ContentRepository.Service.Get<ContentData>(ContentReference.StartPage);

            PageReference containerPageReference = null;

            PropertyInfo translationContainerProperty = GetTranslationContainerProperty(startPageData);

            if (translationContainerProperty != null
                && translationContainerProperty.PropertyType == typeof(PageReference))
            {
                containerPageReference = startPageData.GetPropertyValue(
                    translationContainerProperty.Name,
                    ContentReference.StartPage);
            }

            if (containerPageReference == null)
            {
                containerPageReference =
                    this.ContentRepository.Service.Get<ContentData>(ContentReference.StartPage)
                        .GetPropertyValue("TranslationContainer", ContentReference.StartPage);
            }

            if (containerPageReference != ContentReference.StartPage)
            {
                return containerPageReference;
            }

            Logger.Info("[Localization] No translation container specified.");

            TranslationContainer containerReference =
                this.ContentRepository.Service.GetChildren<PageData>(containerPageReference)
                    .OfType<TranslationContainer>()
                    .FirstOrDefault();

            if (containerReference == null)
            {
                return containerPageReference;
            }

            Logger.Info("[Localization] First translation container used.");

            containerPageReference = containerReference.PageLink;

            return containerPageReference;
        }

        #endregion
    }
}