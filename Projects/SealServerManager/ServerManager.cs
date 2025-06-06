﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Seal.Model;
using System.IO;
using Seal.Helpers;
using Seal.Forms;
using System.Diagnostics;
using System.Threading;
using DocumentFormat.OpenXml.InkML;
using ScintillaNET;

namespace Seal
{
    public partial class ServerManager : Form, IEntityHandler
    {
        #region Members

        TreeViewEditorHelper sourceHelper;
        ToolStripEditorHelper toolStripHelper;
        ToolsHelper toolsHelper;

        ToolStripMenuItem configureMenuItem = new ToolStripMenuItem() { Text = "Configure Server...", ToolTipText = string.Format("Configure the {0} Report Server", Repository.SealRootProductName) };
        ToolStripMenuItem dynamicsMenuItem = new ToolStripMenuItem() { Text = "Edit Dynamics Assemblies...", ToolTipText = "Check the compilation of *.cs files in the Assemblies folder." };
        ToolStripMenuItem publishWebMenuItem = new ToolStripMenuItem() { Text = "Publish Web Site on IIS...", ToolTipText = string.Format("Publish the {0} Web Site on the local Internet Information Server", Repository.SealRootProductName) };
        ToolStripMenuItem securityMenuItem = new ToolStripMenuItem() { Text = "Configure Web Security...", ToolTipText = string.Format("Configure how the reports and folders are published on {0} Web Site", Repository.SealRootProductName) };

        MetaSource _source = null;
        public MetaSource Source
        {
            get { return _source; }
        }

        OutputDevice _device = null;
        public OutputDevice Device
        {
            get { return _device; }
        }


        bool _isModified = false;
        public bool IsModified
        {
            get { return _isModified; }
            set
            {
                _isModified = value;
                enableControls();
            }
        }

        Repository _repository;

        bool _isInitialized = false;
        public bool IsInitialized()
        {
            return _isInitialized;
        }
        public void StartSpashScreen()
        {
            Application.Run(new SplashScreen(this));
        }

        public ServerManager()
        {
            new Thread(new ThreadStart(StartSpashScreen)).Start();

            Repository.IsServerManager = true;

            InitializeComponent();
            mainPropertyGrid.PropertySort = PropertySort.Categorized;
            mainPropertyGrid.LineColor = SystemColors.ControlLight;
            PropertyGridHelper.AddResetMenu(mainPropertyGrid);

            sourceHelper = new TreeViewEditorHelper()
            {
                entityHandler = this,
                resetDisplayOrderToolStripMenuItem = resetDisplayOrderToolStripMenuItem,
                sortColumnAlphaOrderToolStripMenuItem = sortColumnAlphaOrderToolStripMenuItem,
                sortColumnSQLOrderToolStripMenuItem = sortColumnSQLOrderToolStripMenuItem,
                addFromToolStripMenuItem = addFromToolStripMenuItem,
                addToolStripMenuItem = addToolStripMenuItem,
                removeToolStripMenuItem = removeToolStripMenuItem,
                copyToolStripMenuItem = copyToolStripMenuItem,
                removeRootToolStripMenuItem = removeRootToolStripMenuItem,
                treeContextMenuStrip = treeContextMenuStrip,
                mainTreeView = mainTreeView
            };
            toolStripHelper = new ToolStripEditorHelper() { MainToolStrip = mainToolStrip, MainPropertyGrid = mainPropertyGrid, EntityHandler = this, MainTreeView = mainTreeView };
            toolsHelper = new ToolsHelper() { EntityHandler = this };
            toolsHelper.InitHelpers(toolsToolStripMenuItem, false);
            HelperEditor.HandlerInterface = this;

            configureMenuItem.Click += configureClick;
            configurationToolStripMenuItem.DropDownItems.Add(configureMenuItem);
            configureMenuItem.ShortcutKeys = (Keys.Control | Keys.C);

            dynamicsMenuItem.Click += DynamicsMenuItem_Click;
            configurationToolStripMenuItem.DropDownItems.Add(dynamicsMenuItem);

            configurationToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            publishWebMenuItem.Click += configureClick;
            configurationToolStripMenuItem.DropDownItems.Add(publishWebMenuItem);
            publishWebMenuItem.ShortcutKeys = (Keys.Control | Keys.P);

            configurationToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            securityMenuItem.Click += securityClick;
            configurationToolStripMenuItem.DropDownItems.Add(securityMenuItem);
            securityMenuItem.ShortcutKeys = (Keys.Control | Keys.W);

            ShowIcon = true;
            Icon = Properties.Resources.serverManager;

            //Repository management, should be part of the installation
            try
            {
                _repository = Repository.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            //handle program args
            string[] args = Environment.GetCommandLineArgs();
            bool open = (args.Length >= 2 && args[1].ToLower() == "/o");
            string fileToOpen = null;
            if (args.Length >= 3 && File.Exists(args[2])) fileToOpen = args[2];
            //and last used file
            if (!open && File.Exists(Properties.Settings.Default.LastUsedFile))
            {
                open = true;
                fileToOpen = Properties.Settings.Default.LastUsedFile;
            }
            if (open && HasValidRepositoryDirectory(fileToOpen))
            {
                openFile(fileToOpen);
            }
            else
            {
                IsModified = false;
                init();
            }
        }

        private void DynamicsMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "CSharp Files (*.cs)|*.cs";
            dlg.Title = "Select a CSharp file";
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.InitialDirectory = Repository.Instance.DynamicsFolder;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var code = File.ReadAllText(dlg.FileName);
                var frm = new TemplateTextEditorForm();
                frm.SetResetText(code);
                frm.textBox.Text = code;
                frm.checkSyntaxToolStripButton.Visible = true;
                frm.IsRawCSharp = true;
                ScintillaHelper.Init(frm.textBox, Lexer.Cpp);
                if (frm.ShowDialog() == DialogResult.OK)
                {
                     File.WriteAllText(dlg.FileName, frm.textBox.Text);
                }
            }
        }

        bool HasValidRepositoryDirectory(string path)
        {
            return (Path.GetDirectoryName(path).ToLower() == _repository.SourcesFolder.ToLower()
                || Path.GetDirectoryName(path).ToLower() == _repository.DevicesEmailFolder.ToLower()
                || Path.GetDirectoryName(path).ToLower() == _repository.DevicesFileServerFolder.ToLower()
                );
        }

        bool _adminWarningDone = false;
        void configureClick(object sender, EventArgs e)
        {
            _repository.Configuration.ForPublication = (sender == publishWebMenuItem);
            if (_repository.Configuration.ForPublication && string.IsNullOrEmpty(_repository.Configuration.WebPublicationDirectory))
            {

                try
                {
                    Microsoft.Web.Administration.ServerManager serverMgr = new Microsoft.Web.Administration.ServerManager();
                    //try to get default directory...
                    _repository.Configuration.WebPublicationDirectory = Path.Combine(serverMgr.Sites[0].Applications[0].VirtualDirectories[0].PhysicalPath.Replace("%SystemDrive%\\", Path.GetPathRoot(Environment.SystemDirectory)), _repository.Configuration.WebApplicationName.Replace("/", ""));
                }
                catch { }
            }

            if (!_adminWarningDone && !Helper.IsMachineAdministrator())
            {
                if (MessageBox.Show("We recommend to execute the 'Server Manager' application with the option 'Run as administrator' to publish the Web Server application...\r\nDo you want to continue ?", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
                {
                    _adminWarningDone = true;
                    return;
                }
            }

            if (_repository.Configuration.ForPublication)
            {
                if (!Helper.CheckWebServerOS()) return;
            }

            var frm = new ConfigurationEditorForm(_repository.Configuration);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(_repository.Configuration.FilePath)) _repository.Configuration.FilePath = _repository.ConfigurationPath;
                _repository.Configuration.SaveToFile();
            }
            else
            {
                //reload configuration
                _repository.ReloadConfiguration();
            }
        }

        void securityClick(object sender, EventArgs e)
        {
            var frm = new SecurityEditorForm(_repository.Security);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(_repository.Security.FilePath)) _repository.Security.FilePath = _repository.SecurityPath;
                _repository.Security.SaveToFile();
            }
            else
            {
                //reload security
                _repository.ReloadSecurity();
            }
        }

        //EntityHandlerInterface
        public void SetModified()
        {
            IsModified = true;
        }

        public void InitEntity(object entity)
        {
            init(entity);
        }

        public void SimulateKeyPress(Keys key)
        {
        }

        public void RefreshModelTreeView()
        {
        }

        public void UpdateModelNode(TreeNode currentNode = null)
        {
        }

        #endregion

        #region Helpers

        RootComponent selectedEntity
        {
            get
            {
                RootComponent result = null;
                if (mainTreeView.SelectedNode != null && mainTreeView.SelectedNode.Tag is RootComponent) result = (RootComponent)mainTreeView.SelectedNode.Tag;
                return result;
            }
        }

        void selectNode(object entity)
        {
            TreeViewHelper.SelectNode(mainTreeView, mainTreeView.Nodes, entity);
            mainTreeView.SelectedNode?.EnsureVisible();
        }

        void initTreeNodeViews(TreeNode node, ReportView view)
        {
            foreach (var childView in view.Views)
            {
                TreeNode childNode = new TreeNode(childView.Name);
                childNode.Tag = childView;
                node.Nodes.Add(childNode);
                initTreeNodeViews(childNode, childView);
            }
        }

        private void buildOpenMenu(string directory, ToolStripMenuItem menuItem, Image image, bool clear = true)
        {
            if (clear) menuItem.DropDownItems.Clear();
            foreach (var file in Directory.GetFiles(directory, "*." + Repository.SealConfigurationFileExtension))
            {
                ToolStripMenuItem item = new ToolStripMenuItem(Path.GetFileNameWithoutExtension(file));
                item.Image = image;
                item.Tag = file;
                item.Click += new EventHandler(delegate (object sender, EventArgs e)
                {
                    if (!checkModified()) return;
                    openFile((string)((ToolStripMenuItem)sender).Tag);
                });
                menuItem.DropDownItems.Add(item);
            }
        }

        private void buildOpenMenus()
        {
            buildOpenMenu(_repository.SourcesFolder, openSourceToolStripMenuItem, global::Seal.Properties.Resources.database);
            buildOpenMenu(_repository.DevicesEmailFolder, openDeviceToolStripMenuItem, global::Seal.Properties.Resources.device);
            buildOpenMenu(_repository.DevicesFileServerFolder, openDeviceToolStripMenuItem, global::Seal.Properties.Resources.fileserver, false);
        }

        void init(object entityToSelect = null)
        {
            try
            {
                mainTreeView.BeginUpdate();
                //build open menus
                buildOpenMenus();

                mainSplitContainer.Visible = false;
                if (entityToSelect == null && mainTreeView.SelectedNode != null) entityToSelect = mainTreeView.SelectedNode.Tag;
                mainTreeView.Nodes.Clear();

                if (_source != null)
                {
                    mainSplitContainer.Visible = true;
                    sourceHelper.addSource(mainTreeView.Nodes, _source, 9);
                    mainTreeView.TreeViewNodeSorter = new NodeSorter();
                    mainTreeView.Nodes[0].Expand();

                }
                else if (_device != null)
                {
                    mainSplitContainer.Visible = true;
                    var imageIndex = _device is OutputEmailDevice ? 8 : 10;
                    TreeNode mainTN = new TreeNode() { Tag = _device, Text = _device.Name, ImageIndex = imageIndex, SelectedImageIndex = imageIndex };
                    mainTreeView.Nodes.Add(mainTN);
                    mainTreeView.SelectedNode = mainTN;
                }

                if (entityToSelect != null) selectNode(entityToSelect);
                if (mainTreeView.SelectedNode == null && mainTreeView.Nodes.Count > 0) mainTreeView.SelectedNode = mainTreeView.Nodes[0];
                toolsHelper.Source = _source;

                mainTreeView.SelectedNode?.EnsureVisible();
                enableControls();
            }
            finally
            {
                mainTreeView.EndUpdate();
            }
        }

        void enableControls()
        {
            Text = Repository.SealRootProductName + " Server Manager";
            if (Directory.Exists(_repository.RepositoryPath))
            {
                Text = "Repository: " + _repository.RepositoryPath + " - " + Text;
                if (HasEntity)
                {
                    Text = (string.IsNullOrEmpty(FilePath) ? Entity.Name + "*" : Path.GetFileNameWithoutExtension(FilePath) + (IsModified ? "*" : "")) + " - " + Text;
                }
            }

            if (string.IsNullOrEmpty(Repository.Instance.LicenseText))
            {
                Text += " - Free MIT Community License (For non-profit usage or small businesses)";
            }

            saveToolStripMenuItem.Enabled = HasEntity;
            saveToolStripButton.Enabled = HasEntity;
            saveAsToolStripMenuItem.Enabled = HasEntity;
            closeToolStripMenuItem.Enabled = HasEntity;
            openSourceToolStripMenuItem.Enabled = (openSourceToolStripMenuItem.DropDownItems.Count > 0);
            openDeviceToolStripMenuItem.Enabled = (openDeviceToolStripMenuItem.DropDownItems.Count > 0);

            toolsHelper.EnableControls();
        }

        bool HasEntity
        {
            get
            {
                return _source != null || _device != null;
            }
        }

        string FilePath
        {
            get
            {
                if (_source != null) return _source.FilePath;
                if (_device != null) return _device.FilePath;
                return "";
            }
        }

        string FileFolder
        {
            get
            {
                if (_source != null) return _repository.SourcesFolder;
                if (_device != null && _device is OutputEmailDevice) return _repository.DevicesEmailFolder;
                if (_device != null && _device is OutputFileServerDevice) return _repository.DevicesFileServerFolder;
                return "";
            }
        }

        RootComponent Entity
        {
            get
            {
                if (_source != null) return _source;
                if (_device != null) return _device;
                return null;
            }
        }

        bool checkModified()
        {
            bool result = true;
            if (HasEntity && IsModified)
            {
                DialogResult dlgResult = MessageBox.Show("The current file has been modified, do you want to save it ?", "Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (dlgResult == DialogResult.Cancel) result = false;
                if (dlgResult == DialogResult.Yes) saveToolStripMenuItem_Click(null, null);
            }
            return result;
        }

        #endregion

        #region Main Form Handlers

        private void ServerManager_Load(object sender, EventArgs e)
        {
            KeyPreview = true;

            _isInitialized = true;

            _ = Repository.Instance.LicenseText;

            FormHelper.RestoreForm(this, Properties.Settings.Default.FormSize, Properties.Settings.Default.FormLocation, Properties.Settings.Default.FormState);
            BringToFront();
            Activate();

            if (Repository.Instance.LicenseInvalid || string.IsNullOrWhiteSpace(Repository.Instance.LicenseText))
            {
                AboutBoxForm frm = new AboutBoxForm(true);
                frm.ShowDialog(this);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void ServerManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!checkModified()) e.Cancel = true;

            //Save form location and size 
            if (this.WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.FormLocation = this.Location;
                Properties.Settings.Default.FormSize = this.Size;
            }
            else
            {
                Properties.Settings.Default.FormLocation = this.RestoreBounds.Location;
                Properties.Settings.Default.FormSize = this.RestoreBounds.Size;
            }
            // Save the form state as a string
            Properties.Settings.Default.FormState = this.WindowState.ToString();

            Properties.Settings.Default.Save();
        }

        private void openFile(string path)
        {
            bool openOK = true;
            try
            {
                if (Path.GetDirectoryName(path).ToLower() == _repository.SourcesFolder.ToLower())
                {
                    _source = MetaSource.LoadFromFile(path);
                    _source.InitReferences(_repository);
                    _device = null;
                }
                else if (Path.GetDirectoryName(path).ToLower() == _repository.DevicesEmailFolder.ToLower())
                {
                    _source = null;
                    _device = OutputEmailDevice.LoadFromFile(path, false);
                }
                else if (Path.GetDirectoryName(path).ToLower() == _repository.DevicesFileServerFolder.ToLower())
                {
                    _source = null;
                    _device = OutputFileServerDevice.LoadFromFile(path, false);
                }
                else
                {
                    openOK = false;
                    MessageBox.Show("The configuration file must be in a repository folder.\r\nA sub-folder of " + _repository.RepositoryPath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                openOK = false;
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (openOK)
            {
                Properties.Settings.Default.LastUsedFile = path;
            }
            IsModified = false;
            init();
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!checkModified()) return;

            _source = null;
            _device = null;

            object entityToSelect = null;

            if (sender == dataSourceToolStripMenuItem || sender == noSQLdataSourceToolStripMenuItem)
            {
                _source = MetaSource.Create(_repository);
                _source.IsNoSQL = (sender == noSQLdataSourceToolStripMenuItem);
                if (_source.IsNoSQL)
                {
                    entityToSelect = _source.EditorTableFolder;
                    _source.Connection.ConnectionString = "";
                }
                else entityToSelect = _source.Connection;
            }
            else if (sender == emailOutputDeviceToolStripMenuItem)
            {
                _device = OutputEmailDevice.Create();
            }
            else if (sender == fileServerDeviceToolStripMenuItem)
            {
                _device = OutputFileServerDevice.Create();
            }
            IsModified = true;
            init(entityToSelect);
        }


        void validateEntity()
        {
            if (_source != null)
            {
            }
            else if (_device != null)
            {
                _device.Validate();
            }
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_source != null || _device != null)
            {
                if (IsModified)
                {
                    var itemName = _source != null ? "Data Source" : "Device";
                    DialogResult dlgResult = MessageBox.Show($"The {itemName} has been modified, are you sure you to reload it ?", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (dlgResult == DialogResult.Cancel) return;
                }

                string path = _source != null ? _source.FilePath : _device.FilePath;
                _source = null;
                IsModified = false;
                closeToolStripMenuItem_Click(sender, e);
                if (File.Exists(path)) openFile(path);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (HasEntity)
            {
                validateEntity();

                string newPath = "";
                if (sender == saveAsToolStripMenuItem || string.IsNullOrEmpty(Path.GetDirectoryName(FilePath)))
                {
                    newPath = ToolsHelper.SaveConfigurationFile(FileFolder, FilePath, Entity.Name);
                    if (string.IsNullOrEmpty(newPath)) return;
                }

                if (_source != null)
                {
                    if (sender == saveAsToolStripMenuItem) _source.GUID = Guid.NewGuid().ToString(); //New GUID
                    if (!string.IsNullOrEmpty(newPath)) _source.FilePath = newPath;
                    _source.SaveToFile();
                }
                else if (_device != null)
                {
                    if (sender == saveAsToolStripMenuItem) _device.GUID = Guid.NewGuid().ToString(); //New GUID
                    if (!string.IsNullOrEmpty(newPath)) _device.FilePath = newPath;
                    _device.SaveToFile();
                }
                Properties.Settings.Default.LastUsedFile = FilePath;
                Properties.Settings.Default.Save();

                IsModified = false;
                init();
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!checkModified()) return;
            _source = null;
            _device = null;
            IsModified = false;
            toolStripHelper.SetHelperButtons(null);
            init();
        }

        private void ServerManager_KeyDown(object sender, KeyEventArgs e)
        {
            toolStripHelper.HandleShortCut(e);
        }

        #endregion

        #region Tree View Handlers

        private void mainTreeView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Clicks == 1 && e.Button == MouseButtons.Right)
            {
                // Select the clicked node
                var newNode = mainTreeView.GetNodeAt(e.X, e.Y);
                if (newNode != mainTreeView.SelectedNode) mainTreeView.SelectedNode = newNode;

                if (mainTreeView.SelectedNode != null)
                {
                    treeContextMenuStrip.Show(mainTreeView, e.Location);
                }
            }
        }

        private void mainTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right) mainTreeView.SelectedNode = e.Node;
        }


        private void mainTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {

            mainPropertyGrid.SelectedObject = null;
            if (selectedEntity is RootComponent)
            {
                ((RootComponent)selectedEntity).InitEditor();
                mainPropertyGrid.SelectedObject = selectedEntity;

                RootComponent entity = selectedEntity;
                //Do not allow edition of repository objects
                if (selectedEntity is MetaConnection && !((MetaConnection)selectedEntity).IsEditable) entity.SetReadOnly();
                if (selectedEntity is MetaTable && !((MetaTable)selectedEntity).IsEditable) entity.SetReadOnly();
                if (selectedEntity is MetaJoin && !((MetaJoin)selectedEntity).IsEditable) entity.SetReadOnly();
                if (selectedEntity is MetaColumn && !((MetaColumn)selectedEntity).MetaTable.IsEditable) entity.SetReadOnly();
                if (selectedEntity is MetaEnum && !((MetaEnum)selectedEntity).IsEditable) entity.SetReadOnly();
            }

            var entry = Helper.GetGridEntry(mainPropertyGrid, "table parameters");
            if (entry != null) entry.Expanded = true;

            toolStripHelper.SetHelperButtons(selectedEntity);

            //Force shortcuts
            treeContextMenuStrip_Opening(sender, null);
        }

        #endregion

        #region Context Menu Handlers

        private void treeContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            sourceHelper.initTreeContextMenuStrip(new EventHandler(addToolStripMenuItem_Click));
        }


        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            object newEntity = sourceHelper.addToolStripMenuItem_Click(sender, e);
            if (newEntity != null)
            {
                IsModified = true;
                init(newEntity);
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (sourceHelper.removeToolStripMenuItem_Click(sender, e))
            {
                IsModified = true;
                init();
            }
        }

        private void sortColumnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                sourceHelper.sortColumns_Click(sender, e, sender == sortColumnSQLOrderToolStripMenuItem);
                IsModified = true;
                mainTreeView.Sort();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void resetDisplayOrderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                sourceHelper.resetDisplayOrder_Click(sender, e);
                IsModified = true;
                mainTreeView.Sort();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void mainPropertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (sourceHelper.mainPropertyGrid_PropertyValueChanged(s, e)) init();
            if (e.ChangedItem.PropertyDescriptor.Name != "TestEmailTo")
            {
                IsModified = true;
            }
            enableControls();
        }

        private void addFromToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sourceHelper.addFromToolStripMenuItem_Click(sender, e))
            {
                IsModified = true;
                init();
            }
        }

        private void openFolderToolStripButton_Click(object sender, EventArgs e)
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(_repository.RepositoryPath) { UseShellExecute = true };
            p.Start();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            object newEntity = sourceHelper.copyToolStripMenuItem_Click(sender, e);
            if (newEntity != null)
            {
                IsModified = true;
                init(newEntity);
            }
        }

        private void removeRootToolStripMenuItem_Click(object sender, EventArgs e)
        {
            object newEntity = sourceHelper.removeRootToolStripMenuItem_Click(sender, e);
            if (newEntity != null)
            {
                IsModified = true;
                init(newEntity);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBoxForm frm = new AboutBoxForm();
            frm.ShowDialog(this);
        }

        private void openTasksToolStripButton_Click(object sender, EventArgs e)
        {
            if (sender == openTasksToolStripButton)
            {
                if (Helper.CheckTaskSchedulerOS())
                {
                    var p = new Process();
                    p.StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "taskschd.msc"), "/s") { UseShellExecute = true };
                    p.Start();
                }
            }
            else if (sender == openEventsToolStripButton)
            {
                var p = new Process();
                p.StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "eventvwr.msc"), "/s") { UseShellExecute = true };
                p.Start();
            }
        }
        #endregion

        #region Drag and drop
        private void mainTreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            sourceHelper.mainTreeView_ItemDrag(this, sender, e);
        }

        private void mainTreeView_DragEnter(object sender, DragEventArgs e)
        {
            sourceHelper.mainTreeView_DragEnter(sender, e);
        }

        TreeNode _lastDragOverNode = null;
        private void mainTreeView_DragDrop(object sender, DragEventArgs e)
        {
            _lastDragOverNode = null;
            sourceHelper.mainTreeView_DragDrop(sender, e);
        }

        private void mainTreeView_DragOver(object sender, DragEventArgs e)
        {
            sourceHelper.mainTreeView_DragOver(sender, e);
            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                TreeNode targetNode = ((TreeView)sender).GetNodeAt(((TreeView)sender).PointToClient(new Point(e.X, e.Y)));
                _lastDragOverNode = null;
                if (targetNode != null && e.Effect == DragDropEffects.Move)
                {
                    _lastDragOverNode = targetNode;
                    mainTreeView.SelectedNode = targetNode;
                    mainTreeView.SelectedNode?.EnsureVisible();
                }
            }
        }

        #endregion

    }
}
