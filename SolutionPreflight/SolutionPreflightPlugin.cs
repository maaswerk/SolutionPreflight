using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SolutionPreflight
{
    [Export(typeof(IXrmToolBoxPlugin)),
     ExportMetadata("Name", "Solution Preflight"),
     ExportMetadata("Description", "Analyze a Dataverse solution against a target environment before import - missing " +
                                    "components, connection references, Cloud Flow activation, environment variables, " +
                                    "solution layers and more - plus bulk removal of active solution layers."),
     ExportMetadata("SmallImageBase64", "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAADnSURBVFhH7dWxDYQwDABApvttWOIXoGYJ0rAAEhUNNaJGaegYwK/oFUiMHfxF4AtbcgNxdDiOKF7vFp7MAj+4OxWgAAX8O6CDZgUylr7b15X9hl8HsUFT430lAGPxTlHIAd8I1wsAIwypYmNZAPec6wQNqGdY9kILFX6PkgPgDxnMuZYGoEKYRmLNkRkALVRTUOljnaEk1nKA+AjoTrIAlyTCBeqIZAipr78E+CQhASIJuDg+EcBnDDlayh2BJH8CcLcjA8BN73loog4EA5kJkI5wqDIA+H8AdRUzAO5LBShAAQp4HPABOJ7FW5SlWcAAAAAASUVORK5CYII="),
     ExportMetadata("BigImageBase64", "iVBORw0KGgoAAAANSUhEUgAAAFAAAABQCAYAAACOEfKtAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAKCSURBVHhe7Zk9bgIxEIU5XW7DJXIBai4BTS6AREVDjagRDR0HcOQNC7vG9ozn2RuJfU/6mihe25/HP1EWX98/jthZhD8gZVAgCAWCUCAIBYJQIAgFglAgCAWCUCAIBYJQIAgFglAgCAWCUCAIBYJQIAgFglAgCAWCUCBIG4Hbq9Pl7jbrSPuQ9dldwqalOR3fv1uBegLV0hK5nd0y/GZPDYGDXPa79z6M4AJrTW5CgX+5ulXYjwFMIFp1w0wu0Ed5hGSwC6w9qX8RKPSrwChw5za3cCRgchNpKdA5d9hG+lRiE6jcuvnD+ugOw18GBCb7UY4z27eASeByfw+HME7pgLygXBurwA7NbrGfhSaBq1M4gHGQLREFEugJqj0S65gNAqUVta9mElhgu0VvIFA3oSIqCJSOHc03YhgEyqvpYx1QlAkETliB8mBeqfParyFQWvRJBWoO5XFAkahAoT1ybhsFllThOKaVFgTkBcpndvGza4BZoEfaFrnkJx1gFah8SJsW9QEk0INIVG8dQSAUoPo8sMAO5UqnIlZAM4HKBcxQR+ADpBqzEpsIxOV5qgrssV0wmQlVFwi+CgY0EfikdOKp/1uUfieTbKUbaCuwRy0gURnq9vEkb+kKTCOwQ/EeS21jQWBLQRITCvTIf8FEtxgFvpBu6lkIXJ0SZ5WItI1nsoWfVVT6ihcf3HMTOEzqCdIhn31dUgsyC4EVEj3/PBSoSKr6PBQoRbiUKDCTXOX1fJpA9aWQTeLGjfF5AsfoK1LYqik+XeCcoUAQCgShQBAKBKFAEAoEoUAQCgShQBAKBKFAEAoEoUAQCgShQBAKBKFAEAoEoUAQCgShQBAKBPkFTd6+U45RsqoAAAAASUVORK5CYII="),
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
