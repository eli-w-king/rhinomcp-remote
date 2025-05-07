using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Newtonsoft.Json.Linq;

namespace RhinoMCPPlugin.Functions
{
    public partial class RhinoMCPFunctions
    {
        public JObject ExecuteFunction(string functionName, JToken arguments)
        {
            try
            {
                RhinoApp.WriteLine($"Executing function: {functionName}");
                
                // Convert arguments to JObject if it's not null
                JObject argsObj = arguments as JObject;
                if (arguments != null && argsObj == null)
                {
                    // If arguments is not a JObject, try to convert it
                    try {
                        argsObj = JObject.FromObject(arguments);
                    }
                    catch {
                        // If conversion fails, create an empty object
                        argsObj = new JObject();
                    }
                }
                
                // Use empty object if null
                if (argsObj == null)
                {
                    argsObj = new JObject();
                }

                switch (functionName.ToLower())
                {
                    case "createlayer":
                        return CreateLayer(argsObj);
                    case "createobject":
                        return CreateObject(argsObj);
                    case "createobjects":
                        return CreateObjects(argsObj);
                    case "deletelayer":
                        return DeleteLayer(argsObj);
                    case "deleteobject":
                        return DeleteObject(argsObj);
                    case "getdocumentinfo":
                        return GetDocumentInfo(argsObj);
                    case "getobjectinfo":
                        return GetObjectInfo(argsObj);
                    case "getselectedobjectsinfo":
                        return GetSelectedObjectsInfo(argsObj);
                    case "getorsetcurrentlayer":
                        return GetOrSetCurrentLayer(argsObj);
                    case "modifyobject":
                        return ModifyObject(argsObj);
                    case "modifyobjects":
                        return ModifyObjects(argsObj);
                    case "selectobjects":
                        return SelectObjects(argsObj);
                    case "executerhinoscript":
                        return ExecuteRhinoscript(argsObj);
                    default:
                        return JObject.FromObject(new { error = $"Unknown function: {functionName}" });
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error executing function {functionName}: {ex.Message}");
                return JObject.FromObject(new { error = ex.Message });
            }
        }
    }
}
