using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Linq;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using System.IO;
using System.Collections.Generic;

namespace Physique.VS2010Addin
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad("{f1536ef8-92ec-443c-9ed7-fdadf150da82}")]
    [Guid(GuidList.guidVS2010AddinPkgString)]
    public sealed class VS2010AddinPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public VS2010AddinPackage()
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }



        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation
        #region Package Members

        protected override void OnLoadOptions(string key, System.IO.Stream stream)
        {
            base.OnLoadOptions(key, stream);
        }

        const int MAX_TARGETS = 20;

        OleMenuCommand runTargetMenu;
        List<OleMenuCommand> targetCommands;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                var runTargetMenuId = new CommandID(GuidList.guidVS2010AddinCmdSet, (int)PkgCmdIDList.cmdidRunTargetMenu);
                runTargetMenu = new OleMenuCommand(null, runTargetMenuId);
                runTargetMenu.BeforeQueryStatus += new EventHandler(RunTargetMenu_BeforeQueryStatus);
                mcs.AddCommand(runTargetMenu);

                for (int i = 0; i < MAX_TARGETS; i++)
                {
                    var targetDummyId = new CommandID(GuidList.guidVS2010AddinCmdSet, PkgCmdIDList.cmdidRunTargetDynamicDummy + i);
                    var targetDummy = new OleMenuCommand(new EventHandler(RunTargetItem_Invoke), targetDummyId)
                    {
                        Visible = false
                    };
                    targetDummy.BeforeQueryStatus += new EventHandler(RunTargetItem_BeforeQueryStatus);
                    mcs.AddCommand(targetDummy);
                    targetCommands.Add(targetDummy);
                }
            }
        }

        #endregion

        void RunTargetMenu_BeforeQueryStatus(object sender, EventArgs e)
        {
            var file = GetCurrentSelectedItem();
            if (file != null && IsMsBuildFile(file))
            {
                runTargetMenu.Visible = true;
            }
            else
            {
                runTargetMenu.Visible = false;
            }
        }

        void RunTargetItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            var file = GetCurrentSelectedItem();
            if (file != null && IsMsBuildFile(file))
            {
                var project = new Microsoft.Build.Evaluation.Project(file);
                var targets = project.Targets.Keys.ToArray();
                Debug.Assert(MAX_TARGETS > targets.Length);
                for (int i = 0; i < MAX_TARGETS; i++)
                {
                    if (i > targets.Length && targetCommands[i].Visible == false)
                    {
                        break;
                    }
                    else if (i < targets.Length)
                    {
                        targetCommands[i].Visible = true;
                        targetCommands[i].Text = targets[i];
                    }
                    else
                    {
                        targetCommands[i].Visible = false;
                    }
                }
            }
        }

        private void RunTargetItem_Invoke(object sender, EventArgs e)
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "VS2010Addin",
                       string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        private string GetCurrentSelectedItem()
        {
            var DTE = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (DTE.SelectedItems.Count > 0)
            {
                var item = DTE.SelectedItems.Item(1);
                if (item.Project != null)
                {
                    return item.Project.FullName;
                }
                else if (item.ProjectItem != null)
                {
                    Debug.Assert(item.ProjectItem.FileCount == 1);
                    return item.ProjectItem.FileNames[0];
                }
            }

            return null;
        }

        private bool IsMsBuildFile(string file)
        {
            var ext = Path.GetExtension(file);
            return ext.EndsWith("proj");
        }
    }
}
