﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.Shell;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.Internal.VisualStudio.PlatformUI;
using VSXtra.ProjectSystem;

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

        TaskProvider taskProvider;
        IVsOutputWindowPane outputPane;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            taskProvider = new TaskProvider(this);

            var outputWindowService = GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindowService != null)
            {
                Guid guidPane = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid;
                if (outputWindowService.GetPane(ref guidPane, out outputPane) != VSConstants.S_OK)
                {
                    throw new NotSupportedException();
                }
            }

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                InitializeMenus(mcs);
            }
        }

        #endregion

        const int MAX_TARGETS = 20;

        OleMenuCommand runTargetMenu;
        List<OleMenuCommand> targetCommands = new List<OleMenuCommand>();

        ProjectCollection loadedProjects = new ProjectCollection();
        Project project;
        ProjectTargetInstance[] targets;
        IVsHierarchy hierarchy;

        private void InitializeMenus(OleMenuCommandService mcs)
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

            var runTargetOtherMenuId = new CommandID(GuidList.guidVS2010AddinCmdSet, (int)PkgCmdIDList.cmdidRunTargetOther);
            var runTargetOtherMenu = new OleMenuCommand(runTargetOtherMenu_clicked, runTargetOtherMenuId);
            mcs.AddCommand(runTargetOtherMenu);
        }

        void RunTargetMenu_BeforeQueryStatus(object sender, EventArgs e)
        {
            var file = GetCurrentSelectedItem(out hierarchy);
            if (file != null && IsMsBuildFile(file))
            {
                runTargetMenu.Visible = true;
                loadedProjects.UnloadAllProjects();
                project = loadedProjects.LoadProject(file);
                targets = (from target in project.Targets.Values
                           where !target.Name.Substring(1).Any(ch => char.IsUpper(ch))
                                 || !Path.GetFileName(target.FullPath).StartsWith("Microsoft")
                           select target).ToArray();
                Debug.Assert(MAX_TARGETS > targets.Length);
            }
            else
            {
                runTargetMenu.Visible = false;
                targets = null;
            }
        }

        void RunTargetItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            if (targets != null)
            {
                var command = (OleMenuCommand)sender;
                var index = command.CommandID.ID - PkgCmdIDList.cmdidRunTargetDynamicDummy;
                if (index < targets.Length)
                {
                    command.Text = targets[index].Name;
                    command.Visible = true;
                }
                else
                {
                    command.Visible = false;
                }
            }
        }

        private void RunTargetItem_Invoke(object sender, EventArgs e)
        {
            if (targets != null)
            {
                var command = (OleMenuCommand)sender;
                var index = command.CommandID.ID - PkgCmdIDList.cmdidRunTargetDynamicDummy;
                if (index < targets.Length)
                {
                    var target = targets[index];
                    ExecuteTarget(target);
                }
            }
        }

        private void ExecuteTarget(ProjectTargetInstance target)
        {
            IVsBuildManagerAccessor accessor = GetService(typeof(SVsBuildManagerAccessor)) as IVsBuildManagerAccessor;

            if (accessor.ClaimUIThreadForBuild() != VSConstants.S_OK)
            {
                throw new NotImplementedException();
            }

            try
            {
                outputPane.Activate();
                outputPane.Clear();

                outputPane.OutputString(string.Format("------ Run target started: Project: {0}, Target: {1} ------" + Environment.NewLine,
                    project.FullPath,
                    target.Name));

                var buildManager = BuildManager.DefaultBuildManager;
                buildManager.BeginBuild(new BuildParameters
                {
                    Loggers = new[] {
                        new IDEBuildLogger(
                            outputPane,
                            taskProvider,
                            hierarchy)
                    }
                });

                BuildRequestData requestData = new BuildRequestData(
                    project.CreateProjectInstance(),
                    new[] { target.Name },
                    null,
                    BuildRequestDataFlags.ReplaceExistingProjectInstance
                );
                BuildManager.DefaultBuildManager
                    .PendBuildRequest(requestData)
                    .ExecuteAsync((submission) =>
                    {
                        buildManager.EndBuild();
                        var targetResults = submission.BuildResult.ResultsByTarget.Values;

                        outputPane.OutputString(string.Format("========== Build: {0} succeeded, {1} failed, {2} skipped ==========" + Environment.NewLine,
                            targetResults.Count(tr => tr.ResultCode == TargetResultCode.Success),
                            targetResults.Count(tr => tr.ResultCode == TargetResultCode.Failure),
                            targetResults.Count(tr => tr.ResultCode == TargetResultCode.Skipped)));
                    }, null);

                //BuildResult buildResult = submission.Execute();

                //buildManager.EndBuild();
            }
            finally
            {
                accessor.ReleaseUIThreadForBuild();
            }
        }

        private string GetCurrentSelectedItem(out IVsHierarchy hierarchy)
        {
            var sel = GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            IntPtr hierPtr, ppSC;
            uint itemId;
            IVsMultiItemSelect mis;
            var result = sel.GetCurrentSelection(out hierPtr, out itemId, out mis, out ppSC);
            hierarchy = Marshal.GetObjectForIUnknown(hierPtr) as IVsHierarchy;

            object dteObject;
            ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(
                itemId,
                (int)__VSHPROPID.VSHPROPID_ExtObject,
                out dteObject));

            if (dteObject is EnvDTE.Project)
            {
                return ((EnvDTE.Project)dteObject).FullName;
            }
            else if (dteObject is EnvDTE.ProjectItem)
            {
                return ((EnvDTE.ProjectItem)dteObject).FileNames[0];
            }

            return null;
        }

        private bool IsMsBuildFile(string file)
        {
            var ext = Path.GetExtension(file);
            return ext.EndsWith("proj");
        }

        public void runTargetOtherMenu_clicked(object sender, EventArgs e)
        {
            var dialog = new TargetSelectionDialog(project);
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ExecuteTarget(dialog.SelectedTarget);
            }
        }
    }
}
