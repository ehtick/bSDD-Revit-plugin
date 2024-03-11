﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using BIM.IFC.Export.UI;
using BsddRevitPlugin.Logic.IfcExport;
using BsddRevitPlugin.Logic.Model;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Document = Autodesk.Revit.DB.Document;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;


namespace BsddRevitPlugin.Common.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class IFCexporter : IExternalCommand
    {
        public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            try
            {

                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;
                ElementId activeViewId = uiDoc.ActiveView.Id;

                IfcExportManager ifcexportManager = new IfcExportManager();

                //Create an Instance of the IFC Export Class
                IFCExportOptions ifcExportOptions = new IFCExportOptions();

                //Get the bsdd confguration from document or create a new one
                IFCExportConfiguration bsddIFCExportConfiguration = ifcexportManager.GetOrSetBsddConfiguration(doc);

                //Somehow UpdateOptions() can't handle the activeViewId, so we set it manually to -1
                bsddIFCExportConfiguration.ActivePhaseId = -1;

                // Create an instance of the IFCCommandOverrideApplication class
                IFCCommandOverrideApplication ifcCommandOverrideApplication = new IFCCommandOverrideApplication();

                ////Trying to fix phases
                ////Also check https://forums.autodesk.com/t5/revit-api-forum/ifc-export-options/td-p/9686404
                //// Create an instance of CommandEvent
                //CommandEventArgs commandEvent = null;
                //// Call the OnIFCExport methods
                //ifcCommandOverrideApplication.OnIFCExport(uiApp, commandEvent);



                using (Transaction transaction = new Transaction(doc, "Export IFC"))
                {

                    IfcClassificationManager.UpdateClassifications(new Transaction(doc, "Update Classifications"), doc, IfcClassificationManager.GetAllIfcClassificationsInProject());

                    //Set IFC version
                    string IFCversion = "IFC 2x3";

                    //Maak string van alle parameters beginnend met bsdd voor de Export User Defined Propertysets
                    // Create a string to hold all parameters starting with bsdd for the Export User Defined Propertysets
                    string add_BSDD_UDPS = null;

                    // Create a list to hold all BSDD parameters
                    IList<Parameter> param = new List<Parameter>();

                    // Get all BSDD parameters from the document
                    param = ifcexportManager.GetAllBsddParameters(doc);

                    // Organize the BSDD parameters by property set name
                    var organizedParameters = ifcexportManager.RearrageParamatersForEachPropertySet(param);

                    // Loop through all property sets
                    foreach (var parameters in organizedParameters)
                    {
                        //Start with 1 epmty line
                        add_BSDD_UDPS += System.Environment.NewLine + "#" + System.Environment.NewLine + "#";

                        // Format:
                        // #
                        // #
                        // PropertySet:	<Pset Name>	I[nstance]/T[ype]	<element list separated by ','>
                        // #
                        // <Property Name 1>	<Data type>	<[opt] Revit parameter name, if different from IFC>
                        // <Property Name 2>	<Data type>	<[opt] Revit parameter name, if different from IFC>
                        // ...
                        // Add the initial format for the property set to the string
                        add_BSDD_UDPS += System.Environment.NewLine + $"PropertySet:\t{parameters.Key}\tT\tIfcElementType";
                        add_BSDD_UDPS += System.Environment.NewLine + "#" + System.Environment.NewLine + "#\tThis propertyset has been generated by the BSDD Revit plugin" + System.Environment.NewLine + "#" + System.Environment.NewLine;

                        // Loop through all parameters
                        foreach (Parameter p in parameters.Value)
                        {
                            string parameterName = p.Definition.Name.ToString();

                            // Split the definition name by '/'
                            string[] parts = parameterName.Split('/');

                            // Check if there are at least 3 parts
                            if (parts.Length >= 4)
                            {
                                // Get the property set name
                                add_BSDD_UDPS += "\t" + parts[3] + "\t";
                            }
                            else
                            {
                                // Get the property set name
                                add_BSDD_UDPS += "\t" + parameterName + "\t";
                            }


                            //datatypes convert 
                            //C# byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, char, bool, object, string, DataTime
                            //Ifc Area, Boolean, ClassificationReference, ColorTemperature, Count, Currency, 
                            //ElectricalCurrent, ElectricalEfficacy, ElectricalVoltage, Force, Frequency, Identifier, 
                            //Illuminance, Integer, Label, Length, Logical, LuminousFlux, LuminousIntensity, 
                            //NormalisedRatio, PlaneAngle, PositiveLength, PositivePlaneAngle, PositiveRatio, Power, 
                            //Pressure, Ratio, Real, Text, ThermalTransmittance, ThermodynamicTemperature, Volume, 
                            //VolumetricFlowRate

                            // Convert the parameter data type to the corresponding IFC data type and add it to the string
                            if (p.StorageType.ToString() == "String")
                            {
                                add_BSDD_UDPS += "Text";
                            }
                            else if (p.StorageType.ToString() == "Double")
                            {
                                add_BSDD_UDPS += "Real";
                            }
                            else if (p.StorageType.ToString() == "Integer")
                            {
                                var forgeType = p.Definition.GetDataType();
                                if (forgeType.TypeId == "autodesk.spec:spec.bool-1.0.0")
                                {

                                    add_BSDD_UDPS += "Boolean";
                                }
                                else
                                {
                                    add_BSDD_UDPS += "Integer";

                                }
                            }
                            else
                            {
                                add_BSDD_UDPS += p.StorageType.ToString();
                            }


                            // Add the parameter name to the string
                            add_BSDD_UDPS += "\t" + parameterName;

                            // Add a new line to the string
                            add_BSDD_UDPS += System.Environment.NewLine;

                        }
                        // Create a string of all parameters starting with bsdd for the Export User Defined Propertysets
                    }

                    // Start the IFC-transaction
                    transaction.Start("Export IFC");

                    //Create a new temp file for the user defined parameter mapping file
                    string randomFileName = System.IO.Path.GetRandomFileName();
                    string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), randomFileName.Remove(randomFileName.Length - 4) + ".txt");


                    #region exportOptions hardcoded
                    // Start IFC Export Options (when
                    IFCExportOptions exportOptions = new IFCExportOptions();



                    // IFC VERSION METHOD 1 //
                    if (IFCversion == "IFC 2x2")
                    { exportOptions.FileVersion = IFCVersion.IFC2x2; }
                    else if (IFCversion == "IFC 4")
                    { exportOptions.FileVersion = IFCVersion.IFC4; }
                    else
                    { exportOptions.FileVersion = IFCVersion.IFC2x3; }
                    // IFC VERSION METHOD 1 //


                    // IFC VERSION METHOD 2 //
                    //exportOptions.AddOption("IFCVersion", 21.ToString());

                    //IFC2x3 - Coordination View 2.0: 20
                    //IFC2x3 - 21 BASIC
                    //IFC2x3 - Coordination View 2.0 (AutoCAD): 22
                    //IFC2x3 - GSA Concept Design BIM 2010: 23
                    //IFC2x3 - Industry Foundation Classes (IFC2x3): 24
                    //IFC2x3 - Coordination View 2.0 (Nemetschek Allplan): 25
                    //IFC2x3 - Coordination View 2.0 (GRAPHISOFT ArchiCAD): 26
                    //IFC2x3 - Coordination View 2.0 (Tekla Structures): 27
                    //IFC2x3 - Structural Analysis View 2.0: 28
                    //IFC2x3 - Coordination View 2.0 (Dassault Systems CATIA): 29
                    //IFC2x3 - Reference View 2.0: 30
                    //IFC4 - Reference View 1.2 (Experimental): 31
                    //IFC4 - Addendum 2 (Experimental): 32
                    //IFC4 - Addendum 2 (Export as IFC4): 33
                    //IFC4 - Reference View 1.2 (Export as IFC4): 34
                    //IFC4 - Addendum 2 (Experimental Import): 35
                    //IFC4 - Addendum 2 (Export as IFC2x3): 36
                    //IFC4 - Addendum 2 (Reference View 1.2 Import): 37
                    //IFC4 - Addendum 2 (Reference View 1.2 Export as IFC2x3): 38
                    //IFC4 - Addendum 2 (Reference View 1.2 Export as IFC4): 39
                    //IFC4 - Design Transfer View 1.2: 40
                    //IFC4 - Reference View 1.2 (Export as IFC2x3): 41
                    //IFC4 - Reference View 1.2 (Export as IFC4): 42
                    //IFC4 - Addendum 2 (Export as IFC4): 43
                    //IFC4 - Coordination View 2.0: 44
                    //IFC4 - Reference View 1.2: 45
                    //IFC4 - Design Transfer View 1.2 (Export as IFC4): 46
                    // IFC VERSION METHOD 2 //

                    exportOptions.AddOption("ExchangeRequirement", 3.ToString());
                    exportOptions.AddOption("IFCFileType", 0.ToString());
                    //exportOptions.AddOption("ActivePhaseId", 86961.ToString());
                    exportOptions.AddOption("SpaceBoundaries", 1.ToString());
                    exportOptions.AddOption("SplitWallsAndColumns", false.ToString());
                    exportOptions.AddOption("IncludeSteelElements", false.ToString());
                    exportOptions.AddOption("Export2DElements", false.ToString());
                    exportOptions.AddOption("ExportLinkedFiles", false.ToString());
                    exportOptions.AddOption("VisibleElementsOfCurrentView", true.ToString());
                    exportOptions.AddOption("ExportRoomsInView", false.ToString());
                    exportOptions.AddOption("ExportInternalRevitPropertySets", false.ToString());
                    exportOptions.AddOption("ExportIFCCommonPropertySets", true.ToString());
                    exportOptions.AddOption("ExportBaseQuantities", true.ToString());
                    exportOptions.AddOption("ExportSchedulesAsPsets", false.ToString());
                    exportOptions.AddOption("ExportSpecificSchedules", false.ToString());

                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    exportOptions.AddOption("ExportUserDefinedPsets", true.ToString());
                    exportOptions.AddOption("ExportUserDefinedPsetsFileName", tempFilePath);
                    exportOptions.AddOption("ExportInternalRevitPropertySets", false.ToString());
                    exportOptions.AddOption("ExportUserDefinedParameterMapping", true.ToString());
                    exportOptions.AddOption("ExportUserDefinedParameterMappingFileName", tempFilePath);
                    exportOptions.AddOption("TessellationLevelOfDetail", 0.5.ToString());
                    exportOptions.AddOption("ExportPartsAsBuildingElements", false.ToString());
                    exportOptions.AddOption("ExportSolidModelRep", false.ToString());
                    exportOptions.AddOption("UseActiveViewGeometry", true.ToString());
                    exportOptions.AddOption("UseFamilyAndTypeNameForReference", true.ToString());
                    exportOptions.AddOption("Use2DRoomBoundaryForVolume", false.ToString());
                    exportOptions.AddOption("IncludeSiteElevation", false.ToString());
                    exportOptions.AddOption("StoreIFCGUID", true.ToString());
                    exportOptions.AddOption("ExportBoundingBox", false.ToString());
                    exportOptions.AddOption("UseOnlyTriangulation", false.ToString());
                    exportOptions.AddOption("UseTypeNameOnlyForIfcType", true.ToString());
                    exportOptions.AddOption("UseVisibleRevitNameAsEntityName", true.ToString());
                    exportOptions.FilterViewId = activeViewId;
                    //exportOptions.AddOption("SelectedSite", "MF");
                    //exportOptions.AddOption("SitePlacement", 0.ToString());
                    //exportOptions.AddOption("GeoRefCRSName", "");
                    //exportOptions.AddOption("GeoRefCRSDesc", "");
                    //exportOptions.AddOption("GeoRefEPSGCode", "");
                    //exportOptions.AddOption("GeoRefGeodeticDatum", "");
                    //exportOptions.AddOption("GeoRefMapUnit", "");
                    //exportOptions.AddOption("ExcludeFilter", "");
                    //exportOptions.AddOption("COBieCompanyInfo", "");
                    //exportOptions.AddOption("COBieProjectInfo", "");
                    //exportOptions.AddOption("Name", "Setup 1");

                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    //exportOptions.AddOption("ExportUserDefinedPsets", false.ToString());
                    //exportOptions.AddOption("ExportUserDefinedPsetsFileName", "");

                    exportOptions.AddOption("ExportInternalRevitPropertySets", false.ToString());
                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                    #endregion


                    // Get the selected file path
                    string mappingParameterFilePath = bsddIFCExportConfiguration.ExportUserDefinedParameterMappingFileName;

                    //Copy user defined parameter mapping file to temp file
                    if (File.Exists(mappingParameterFilePath))
                    {
                        File.Copy(mappingParameterFilePath, tempFilePath, true);
                    }


                    using (StreamWriter writer = new StreamWriter(tempFilePath, true))
                    {
                        writer.WriteLine(add_BSDD_UDPS);
                    }

                    //IFCExportOptions.AddOption("ExportUserDefinedParameterMapping", true.ToString());
                    //IFCExportOptions.AddOption("ExportUserDefinedPsetsFileName", tempFilePath.ToString());

                    bsddIFCExportConfiguration.ExportUserDefinedPsets = true;
                    bsddIFCExportConfiguration.ExportUserDefinedPsetsFileName = tempFilePath;

                    //Pass the setting of the myIFCExportConfiguration to the IFCExportOptions
                    bsddIFCExportConfiguration.UpdateOptions(ifcExportOptions, activeViewId);

                    //// Add option with a new IFC Class System
                    //using (var form = new System.Windows.Forms.Form())
                    //{
                    //    // Create OpenFileDialog
                    //    TaskDialog.Show("Export Layers", "Pick a file for Export Layers");
                    //    OpenFileDialog openFileDialog = new OpenFileDialog();
                    //    openFileDialog.Filter = "Text Files (*.txt)|*.txt";
                    //    openFileDialog.FilterIndex = 1;
                    //    openFileDialog.Multiselect = false;

                    //    // Show OpenFileDialog and get the result
                    //    DialogResult result = openFileDialog.ShowDialog(form);

                    //    // Check if the user clicked OK in the OpenFileDialog
                    //    if (result == DialogResult.OK)
                    //    {
                    //        // Get the selected file path
                    //        string mappingFilePath = openFileDialog.FileName;

                    //        // Add the option for IFC Export Classes Family Mapping
                    //        exportOptions.AddOption("ExportLayers", mappingFilePath);
                    //    }
                    //}


                    TaskDialog.Show("IFC-Export", "Save IFC As");
                    // Create a SaveFile Dialog to enable a location to export the IFC to
                    SaveFileDialog saveFileDialog = new SaveFileDialog();

                    // Set properties of the SaveFileDialog
                    //saveFileDialog.Filter = "IFC Files (*.ifc)|*.rvt|All Files (*.*)|*.*";
                    saveFileDialog.Filter = "IFC Files (*.ifc)|*.ifc";
                    saveFileDialog.FilterIndex = 1;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Get the selected file path
                        string ifcFilePath = saveFileDialog.FileName;
                        string ifcFileName = Path.GetFileName(ifcFilePath);
                        string directory = Path.GetDirectoryName(ifcFilePath);

                        // Check if the file path is not empty
                        if (!string.IsNullOrEmpty(ifcFilePath))
                        {
                            try
                            {
                                string tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                                Directory.CreateDirectory(tempDirectoryPath);
                                if (!Directory.Exists(tempDirectoryPath))
                                {
                                    throw new Exception("Failed to create temporary directory.");
                                }

                                string tempIfcFilePath = Path.Combine(tempDirectoryPath, Path.GetFileName(ifcFilePath));

                                IfcPostprocessor postprocessor = new IfcPostprocessor();
                                postprocessor.CollectIfcClassifications(doc);

                                doc.Export(tempDirectoryPath, ifcFileName, ifcExportOptions);
                                if (!File.Exists(tempIfcFilePath))
                                {
                                    throw new Exception("Failed to export document.");
                                }
                                transaction.Commit();

                                postprocessor.PostProcess(tempIfcFilePath, ifcFilePath);

                                Directory.Delete(tempDirectoryPath, true);

                                TaskDialog.Show("IFC-Export", "An IFC-export was executed.");
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Error", "An error occurred: " + ex.Message);
                                message = ex.Message;
                                return Result.Failed;
                            }
                        }
                    }


                    System.IO.File.Delete(tempFilePath);
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "An error occurred: " + ex.Message);
                message = ex.Message;
                return Result.Failed;
            }




        }




    }
}