﻿using System;
using System.Web.Mvc;
using CmsData.ExtraValue;
using DocumentFormat.OpenXml.InkML;
using UtilityExtensions;

namespace CmsWeb.Framework
{
    public class CmsViewEngine : IViewEngine
    {
        private const string TOUCHPOINT_EXT = "touch";
        public IViewEngine BaseViewEngine { get; private set; }
        public bool UseTouchPointViews { get; private set; }
         
        public CmsViewEngine(bool useTouchPointViews)
        {
            BaseViewEngine = new RazorViewEngine();
            UseTouchPointViews = useTouchPointViews;  
        }

        public ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache)
        {
            if (UseTouchPointViews)
            {
                var viewEngineResult = BaseViewEngine.FindPartialView(controllerContext, "{0}.{1}".Fmt(partialViewName, TOUCHPOINT_EXT), useCache);
                if (viewEngineResult.View != null)
                    return viewEngineResult;
            }

            // otherwise we just do the default find partial view.
            return BaseViewEngine.FindPartialView(controllerContext, partialViewName, useCache);
        }

        public ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
        {
            if (UseTouchPointViews)
            {
                var viewEngineResult = BaseViewEngine.FindView(controllerContext, "{0}.{1}".Fmt(viewName, TOUCHPOINT_EXT), masterName, useCache);
                if (viewEngineResult.View != null)
                    return viewEngineResult;
            }

            // otherwise we just do the default find view.
            return BaseViewEngine.FindView(controllerContext, viewName, masterName, useCache);
        }

        public void ReleaseView(ControllerContext controllerContext, IView view)
        {
            throw new NotImplementedException();
        }
    }
}