﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BsddRevitPlugin.Logic.IfcJson;
using BsddRevitPlugin.Logic.Model;
using BsddRevitPlugin.Logic.UI.View;
using BsddRevitPlugin.Logic.UI.Wrappers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace BsddRevitPlugin.Logic.UI.BsddBridge
{

    /// <summary>
    /// Represents a bridge for interacting with the bSDD selection UI.
    /// This class is exposed to JavaScript in CefSharp.
    /// </summary>
    public class BsddSelectionBridge
    {
        private ExternalEvent _bsddLastSelectionEvent;
        private EventHandlerBsddSearch _eventHandlerBsddSearch;
        private UpdateElementtypeWithIfcData _updateElementtypeWithIfcData;
        private UpdateSettings _updateSettings;
        private ExternalEvent _exEventUpdateElement;
        private ExternalEvent _exEventUpdateSettings;
        private SelectElementsWithIfcData selectElementsWithIfcData;
        private ExternalEvent _exEventSelectElement;

        /// <summary>
        /// Initializes a new instance of the <see cref="BsddSelectionBridge"/> class.
        /// </summary>
        public BsddSelectionBridge(ExternalEvent bsddLastSelectionExEvent)
        {
            _bsddLastSelectionEvent = bsddLastSelectionExEvent;
            _eventHandlerBsddSearch = new EventHandlerBsddSearch(_bsddLastSelectionEvent);
            _updateSettings = new UpdateSettings();

            _updateElementtypeWithIfcData = new UpdateElementtypeWithIfcData();
            _exEventUpdateElement = ExternalEvent.Create(_updateElementtypeWithIfcData);
            _exEventUpdateSettings = ExternalEvent.Create(_updateSettings);

            selectElementsWithIfcData = new SelectElementsWithIfcData();
            _exEventSelectElement = ExternalEvent.Create(selectElementsWithIfcData);

        }

        /// <summary>
        /// This method is exposed to JavaScript in CefSharp. 
        /// It opens the bSDD Search panel with the selected object parameters.
        /// </summary>
        /// <param name="ifcJsonData">The IFC data to search, in JSON format.</param>
        /// <returns>The serialized IFC data, in JSON format.</returns>
        public string bsddSearch(string ifcJsonData)
        {

            var converter = new IfcJsonConverter();
            var ifcEntity = JsonConvert.DeserializeObject<IfcEntity>(ifcJsonData, converter);
            var bsddBridgeData = new BsddBridgeData
            {
                Settings = GlobalBsddSettings.bsddsettings,
                IfcData = new List<IfcEntity> { ifcEntity }
            };
            _eventHandlerBsddSearch.setBsddBridgeData(bsddBridgeData);
            _eventHandlerBsddSearch.Raise("openSearch");

            return JsonConvert.SerializeObject(ifcEntity);
        }

        /// <summary>
        /// This method is exposed to JavaScript in CefSharp. 
        /// It opens the bSDD Search panel with the selected object parameters.
        /// </summary>
        /// <param name="ifcJsonData">The IFC data to search, in JSON format.</param>
        /// <returns>The serialized IFC data, in JSON format.</returns>
        public void bsddSelect(string ifcJsonData)
        {

            var converter = new IfcJsonConverter();
            var ifcEntity = JsonConvert.DeserializeObject<IfcEntity>(ifcJsonData, converter);
            

            selectElementsWithIfcData.SetIfcData(ifcEntity);
            _exEventSelectElement.Raise();

        }

        /// <summary>
        /// This method is exposed to JavaScript in CefSharp. 
        /// It updates the settings from a JSON string.
        /// </summary>
        /// <param name="settingsJson">The JSON string of the new settings.</param>
        public void saveSettings(string settingsJson)
        {
            var settings = JsonConvert.DeserializeObject<BsddSettings>(settingsJson);

            //set the classificationFieldName for new dictionaries
            settings.MainDictionary.IfcClassification.ClassificationFieldName = ElementsManager.CreateParameterNameFromUri(settings.MainDictionary.IfcClassification.Location);

            foreach (var item in settings.FilterDictionaries)
            {
                item.IfcClassification.ClassificationFieldName = ElementsManager.CreateParameterNameFromUri(item.IfcClassification.Location);

            }
            _updateSettings.SetSettings(settings);
            _exEventUpdateSettings.Raise();

            // Update the selection UI with the last selection
            _bsddLastSelectionEvent.Raise();

        }
        public string loadSettings()
        {
            return JsonConvert.SerializeObject(GlobalBsddSettings.bsddsettings);
        }
    }
}