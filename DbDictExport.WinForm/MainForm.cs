﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DbDictExport.Core;
using DbDictExport.Core.Codes;
using DbDictExport.Core.Common;
using DbDictExport.Core.Dal;
using DbDictExport.WinForm.Service;
using MetroFramework.Forms;

namespace DbDictExport.WinForm
{
    public partial class MainForm : MetroForm
    {

        private SqlConnectionStringBuilder _connBuilder;


        //private string columnTreeNodeNamePrefix = "col_";
        public SqlConnectionStringBuilder ConnBuilder
        {
            get { return _connBuilder; }
            set { _connBuilder = value; }
        }

        private static TreeNode SelectedTableNode { get; set; }

        public MainForm()
        {
            InitializeComponent();
            MetroGridResultSet.DataError += dgvResultSet_DataError;
            tvDatabase.BeforeExpand += tvDatabase_BeforeExpand;
            tvDatabase.MouseDown += tvDatabase_MouseDown;
            foreach (ToolStripItem item in cmsDatabase.Items)
            {
                item.Click += cmsDatabaseItem_Click;
            }
            foreach (ToolStripItem item in cmsDbTable.Items)
            {
                item.Click += cmsDbTableItem_Click;
            }
            LoadLoginForm();
            tvDatabase.ImageList = imgListCommon;
        }




        void dgvResultSet_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (!MetroGridResultSet.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.Equals(DBNull.Value))
            {
                e.ThrowException = false;
            }

        }

        #region database TreeView's events
        void tvDatabase_MouseDown(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Right:
                    {
                        var clickPoint = new Point(e.X, e.Y);
                        TreeNode currentNode = tvDatabase.GetNodeAt(clickPoint);
                        if (currentNode != null)
                        {
                            if (currentNode.Name.StartsWith(Constants.DATABASE_TREE_NODE_NAME_PREFIX))
                            {
                                currentNode.ContextMenuStrip = cmsDatabase;
                            }
                            else if (currentNode.Name.StartsWith(Constants.TABLE_TREE_NODE_NAME_PREFIX))
                            {
                                currentNode.ContextMenuStrip = cmsDbTable;
                            }
                            tvDatabase.SelectedNode = currentNode;
                        }
                    }
                    break;
                case MouseButtons.Left:
                    {
                        var clickPoint = new Point(e.X, e.Y);
                        TreeNode currentNode = tvDatabase.GetNodeAt(clickPoint);
                        if (currentNode != null)
                        {
                            if (currentNode.Name.StartsWith(Constants.TABLE_TREE_NODE_NAME_PREFIX))
                            {
                                ClearGridData();

                                if (SelectedTableNode != null)
                                {
                                    SelectedTableNode.BackColor = Color.White;
                                }
                                SelectedTableNode = currentNode;
                                SelectedTableNode.BackColor = SystemColors.Highlight;

                                var table = currentNode.Tag as DbTable;
                                SetGridData(currentNode.Parent.Text, table.Name);
                            }
                        }
                    }
                    break;
            }
        }

        void tvDatabase_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Name.StartsWith(Constants.DATABASE_TREE_NODE_NAME_PREFIX))
            {
                if (e.Node.Nodes.Count == 1 && String.IsNullOrEmpty(e.Node.Nodes[0].Text))
                {
                    // If has the empty node
                    TreeNode rootNode = e.Node;
                    LoadTableTreeNode(rootNode);
                }
            }
        }
        #endregion

        #region ContextMenuStrip click event

        private void cmsDatabaseItem_Click(object sender, EventArgs e)
        {
            var tripItem = sender as ToolStripItem;
            var currentNode = tvDatabase.SelectedNode;
            if (tripItem == null) return;
            switch (tripItem.Text)
            {
                case Constants.CONTEXT_MENU_DATABASE_EXPORT_DICTIONARY:
                    try
                    {
                        LoadingFormService.CreateForm();
                        LoadingFormService.SetFormCaption(Constants.EXPORT_CAPTION);
                        List<DbTable> tableList = DataAccess.GetDbTableListWithColumns(_connBuilder, currentNode.Text);
                        //Workbook workbook = ExcelHelper.GenerateWorkbook(tableList);
                        IExcelHelper helper = new AsposeExcelHelper();

                        //LoadingFormService.CloseFrom();

                        var dia = new SaveFileDialog
                        {
                            Filter = Constants.SAVE_FILE_DIALOG_FILTER,
                            FileName = currentNode.Text + " Data Dictionary"
                        };
                        if (dia.ShowDialog() == DialogResult.OK)
                        {
                            helper.GenerateWorkbook(tableList, dia.FileName);
                            //workbook.Save(dia.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Constants.ERROR_CAPTION, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        LoadingFormService.CloseFrom();
                    }
                    break;
                case Constants.CONTEXT_MENU_DATABASE_REFRESH:
                    LoadTableTreeNode(currentNode);
                    break;
            }
        }

        private void cmsDbTableItem_Click(object sender, EventArgs eventArgs)
        {
            var tripItem = sender as ToolStripItem;
            var currentNode = tvDatabase.SelectedNode;
            if (tripItem == null) return;
            switch (tripItem.Text)
            {
                case Constants.CONTEXT_MENU_TABLE_GENERATE_KD_CODES:
                    var table = currentNode.Tag as DbTable;
                    if (table == null) break;
                    table.ColumnList = DataAccess.GetDbColumnList(ConnBuilder, table.Name);
                    var form = new KdCodeForm(table);
                    form.Show();
                    break;

            }
        }
        #endregion

        #region MenuItems click events
        private void newConnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadLoginForm();
        }

        #endregion

        #region Load TreeView nodes
        private void LoadTableTreeNode(TreeNode rootNode)
        {
            //if first expand the node
            //clear the empty node
            tvDatabase.Cursor = Cursors.AppStarting;
            rootNode.Nodes.Clear();
            List<DbTable> tableList = DataAccess.GetDbTableNameListWithoutColumns(_connBuilder, rootNode.Text);
            foreach (var tableNode in tableList.Select(table => new TreeNode
            {
                Name = Constants.TABLE_TREE_NODE_NAME_PREFIX + table.Name,
                Text = String.Format("{0}.{1}", table.Schema, table.Name),
                ToolTipText = String.Format("{0}.{1}", table.Schema, table.Name),
                Tag = table,
                ImageIndex = Constants.TREENODE_DATATABLE_IMAGE_INDEX,
                SelectedImageIndex = Constants.TREENODE_DATATABLE_IMAGE_INDEX
            }))
            {
                rootNode.Nodes.Add(tableNode);
            }
            tvDatabase.Cursor = Cursors.Default;

        }

        private void LoadDatabaseTreeNode()
        {
            tvDatabase.Nodes.Clear();
            var rootNode = new TreeNode
            {
                Text = _connBuilder.DataSource + string.Format("({0})", _connBuilder.UserID),
                ImageIndex = Constants.TREENODE_ROOT_IMAGE_INDEX,
                SelectedImageIndex = Constants.TREENODE_ROOT_IMAGE_INDEX
            };
            tvDatabase.Nodes.Add(rootNode);
            foreach (string dbName in DataAccess.GetDbNameList(ConnBuilder))
            {
                var databaseNode = new TreeNode
                {
                    Text = dbName,
                    ToolTipText = dbName,
                    Name = Constants.DATABASE_TREE_NODE_NAME_PREFIX + dbName,
                    ImageIndex = Constants.TREENODE_DATABASE_IMAGE_INDEX,
                    SelectedImageIndex = Constants.TREENODE_DATABASE_IMAGE_INDEX
                };

                /*
                 * The child node will not load with database node when the form loaded,
                 * so here put a empty node which do nothing to every database node
                 * that tell someone there maybe some child nodes.
                 * It will be clear when the specific database node be expended,
                 * and load  real child nodes.
                 * */
                var emptyNode = new TreeNode();
                databaseNode.Nodes.Add(emptyNode);
                rootNode.Nodes.Add(databaseNode);
            }
        }
        #endregion

        private void LoadLoginForm()
        {
            var login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
            {
                _connBuilder = login.ConnBuilder;
                ClearGridData();
                login.Close();
                LoadDatabaseTreeNode();
            }
        }

        private void SetGridData(string dbName, string tableName)
        {
            var table = DataAccess.GetTableByName(_connBuilder, dbName, tableName);
            if (table == null) return;

            MetroGridDesign.DataSource = table.ColumnList.ToDataTable();

            if (dbName == Constants.DATABASE_TEMP_DB_NAME)
            {
                var dgvr = new DataGridViewRow();
                var cell = new DataGridViewTextBoxCell
                {
                    Value = "The temp table do not support viewing records."
                };
                dgvr.Cells.Add(cell);

                MetroGridResultSet.Columns.Add("Message", "Message");
                MetroGridResultSet.Columns["Message"].Width = 600;
                MetroGridResultSet.Rows.Add(dgvr);
                return;
            }
            MetroGridResultSet.DataSource = DataAccess.GetResultSetByDbTable(_connBuilder, table);

            MetroGridDesign.ClearSelection();
            MetroGridResultSet.ClearSelection();
        }

        private void ClearGridData()
        {
            MetroGridResultSet.DataSource = null;
            MetroGridResultSet.Columns.Clear();

            MetroGridDesign.DataSource = null;
            MetroGridDesign.Columns.Clear();
        }
    }

}
