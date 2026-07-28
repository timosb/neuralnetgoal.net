namespace NeuralNetGoalTools
{
    partial class mainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            tabbedView = new DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView(components);
            ribbonStatusBar = new DevExpress.XtraBars.Ribbon.RibbonStatusBar();
            ribbonControl = new DevExpress.XtraBars.Ribbon.RibbonControl();
            skinRibbonGalleryBarItem = new DevExpress.XtraBars.SkinRibbonGalleryBarItem();
            barSubItemNavigation = new DevExpress.XtraBars.BarSubItem();
            queriesBarButtonItem = new DevExpress.XtraBars.BarButtonItem();
            customersBarButtonItem = new DevExpress.XtraBars.BarButtonItem();
            skinDropDownButtonItem = new DevExpress.XtraBars.SkinDropDownButtonItem();
            skinPaletteRibbonGalleryBarItem = new DevExpress.XtraBars.SkinPaletteRibbonGalleryBarItem();
            navigationFrame = new DevExpress.XtraBars.Navigation.NavigationFrame();
            queriesNavigationPage = new DevExpress.XtraBars.Navigation.NavigationPage();
            ((System.ComponentModel.ISupportInitialize)tabbedView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ribbonControl).BeginInit();
            ((System.ComponentModel.ISupportInitialize)navigationFrame).BeginInit();
            navigationFrame.SuspendLayout();
            SuspendLayout();
            // 
            // ribbonStatusBar
            // 
            ribbonStatusBar.Location = new System.Drawing.Point(0, 596);
            ribbonStatusBar.Name = "ribbonStatusBar";
            ribbonStatusBar.Ribbon = ribbonControl;
            ribbonStatusBar.Size = new System.Drawing.Size(1106, 24);
            // 
            // ribbonControl
            // 
            ribbonControl.ExpandCollapseItem.Id = 0;
            ribbonControl.Items.AddRange(new DevExpress.XtraBars.BarItem[] { ribbonControl.ExpandCollapseItem, skinRibbonGalleryBarItem, barSubItemNavigation, queriesBarButtonItem, customersBarButtonItem, skinDropDownButtonItem, skinPaletteRibbonGalleryBarItem });
            ribbonControl.Location = new System.Drawing.Point(0, 0);
            ribbonControl.MaxItemId = 47;
            ribbonControl.MdiMergeStyle = DevExpress.XtraBars.Ribbon.RibbonMdiMergeStyle.Always;
            ribbonControl.Name = "ribbonControl";
            ribbonControl.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonControlStyle.Office2013;
            ribbonControl.ShowApplicationButton = DevExpress.Utils.DefaultBoolean.False;
            ribbonControl.Size = new System.Drawing.Size(1106, 58);
            ribbonControl.StatusBar = ribbonStatusBar;
            ribbonControl.ToolbarLocation = DevExpress.XtraBars.Ribbon.RibbonQuickAccessToolbarLocation.Hidden;
            // 
            // skinRibbonGalleryBarItem
            // 
            skinRibbonGalleryBarItem.Id = 14;
            skinRibbonGalleryBarItem.Name = "skinRibbonGalleryBarItem";
            // 
            // barSubItemNavigation
            // 
            barSubItemNavigation.Caption = "Navigation";
            barSubItemNavigation.Id = 15;
            barSubItemNavigation.ImageOptions.ImageUri.Uri = "NavigationBar";
            barSubItemNavigation.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] { new DevExpress.XtraBars.LinkPersistInfo(queriesBarButtonItem) });
            barSubItemNavigation.Name = "barSubItemNavigation";
            // 
            // queriesBarButtonItem
            // 
            queriesBarButtonItem.Caption = "Queries";
            queriesBarButtonItem.Id = 44;
            queriesBarButtonItem.Name = "queriesBarButtonItem";
            queriesBarButtonItem.ItemClick += barButtonNavigation_ItemClick;
            // 
            // customersBarButtonItem
            // 
            customersBarButtonItem.Caption = "Customers";
            customersBarButtonItem.Id = 45;
            customersBarButtonItem.Name = "customersBarButtonItem";
            customersBarButtonItem.ItemClick += barButtonNavigation_ItemClick;
            // 
            // skinDropDownButtonItem
            // 
            skinDropDownButtonItem.ActAsDropDown = true;
            skinDropDownButtonItem.ButtonStyle = DevExpress.XtraBars.BarButtonStyle.DropDown;
            skinDropDownButtonItem.Id = 46;
            skinDropDownButtonItem.Name = "skinDropDownButtonItem";
            // 
            // skinPaletteRibbonGalleryBarItem
            // 
            skinPaletteRibbonGalleryBarItem.Caption = "$newskinpalettename$";
            skinPaletteRibbonGalleryBarItem.Id = 47;
            skinPaletteRibbonGalleryBarItem.Name = "skinPaletteRibbonGalleryBarItem";
            // 
            // navigationFrame
            // 
            navigationFrame.Appearance.BackColor = System.Drawing.Color.White;
            navigationFrame.Appearance.Options.UseBackColor = true;
            navigationFrame.Controls.Add(queriesNavigationPage);
            navigationFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            navigationFrame.Location = new System.Drawing.Point(0, 58);
            navigationFrame.Name = "navigationFrame";
            navigationFrame.Pages.AddRange(new DevExpress.XtraBars.Navigation.NavigationPageBase[] { queriesNavigationPage });
            navigationFrame.RibbonAndBarsMergeStyle = DevExpress.XtraBars.Docking2010.Views.RibbonAndBarsMergeStyle.Always;
            navigationFrame.SelectedPage = queriesNavigationPage;
            navigationFrame.Size = new System.Drawing.Size(1106, 538);
            navigationFrame.TabIndex = 0;
            navigationFrame.Text = "navigationFrame";
            // 
            // queriesNavigationPage
            // 
            queriesNavigationPage.Caption = "queriesNavigationPage";
            queriesNavigationPage.Name = "queriesNavigationPage";
            queriesNavigationPage.Size = new System.Drawing.Size(1106, 538);
            // 
            // mainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1106, 620);
            Controls.Add(navigationFrame);
            Controls.Add(ribbonStatusBar);
            Controls.Add(ribbonControl);
            IconOptions.LargeImage = Properties.Resources.selectdata_32x321;
            Name = "mainForm";
            Ribbon = ribbonControl;
            StatusBar = ribbonStatusBar;
            ((System.ComponentModel.ISupportInitialize)tabbedView).EndInit();
            ((System.ComponentModel.ISupportInitialize)ribbonControl).EndInit();
            ((System.ComponentModel.ISupportInitialize)navigationFrame).EndInit();
            navigationFrame.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView tabbedView;
        private DevExpress.XtraBars.Ribbon.RibbonStatusBar ribbonStatusBar;
        private DevExpress.XtraBars.Navigation.NavigationFrame navigationFrame;
        private DevExpress.XtraBars.Navigation.NavigationPage queriesNavigationPage;
        private DevExpress.XtraBars.Ribbon.RibbonControl ribbonControl;
        private DevExpress.XtraBars.SkinRibbonGalleryBarItem skinRibbonGalleryBarItem;
        private DevExpress.XtraBars.BarSubItem barSubItemNavigation;
        private DevExpress.XtraBars.BarButtonItem queriesBarButtonItem;
        private DevExpress.XtraBars.BarButtonItem customersBarButtonItem;
        private DevExpress.XtraBars.SkinDropDownButtonItem skinDropDownButtonItem;
        private DevExpress.XtraBars.SkinPaletteRibbonGalleryBarItem skinPaletteRibbonGalleryBarItem;
    }
}