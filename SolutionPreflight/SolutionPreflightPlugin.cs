using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;

namespace SolutionPreflight
{
    [Export(typeof(IXrmToolBoxPlugin)),
     ExportMetadata("Name", "Solution Preflight"),
     ExportMetadata("Description", "Analyze a Dataverse solution against a target environment before import - missing " +
                                    "components, connection references, Cloud Flow activation, environment variables, " +
                                    "solution layers and more - plus bulk removal of active solution layers."),
     ExportMetadata("BackgroundColor", "SteelBlue"),
     ExportMetadata("PrimaryFontColor", "White"),
     ExportMetadata("SecondaryFontColor", "WhiteSmoke")]
    public class SolutionPreflightPlugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SolutionPreflightControl();
        }
    }
}
