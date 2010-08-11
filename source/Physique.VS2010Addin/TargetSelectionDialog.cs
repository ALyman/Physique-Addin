using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Build.Execution;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Physique.VS2010Addin
{
    public partial class TargetSelectionDialog : Form
    {
        private Microsoft.Build.Evaluation.Project project;

        public TargetSelectionDialog(Microsoft.Build.Evaluation.Project project)
        {
            InitializeComponent();

            listView1.BeginUpdate();
            foreach (var target in project.Targets.Values)
            {
                listView1.Items.Add(CreateItem(target));
            }
            listView1.EndUpdate();
        }

        public ProjectTargetInstance SelectedTarget { get; set; }

        private ListViewItem CreateItem(ProjectTargetInstance target)
        {
            return new ListViewItem(new[] { 
                target.Name, 
                target.FullPath
            })
            {
                Tag = target
            };
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SelectedTarget = (ProjectTargetInstance)listView1.SelectedItems[0].Tag;
        }
    }
}
