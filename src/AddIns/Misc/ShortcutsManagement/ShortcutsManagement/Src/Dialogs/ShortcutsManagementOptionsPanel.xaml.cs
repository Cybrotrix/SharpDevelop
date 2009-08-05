﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.Core;
using ICSharpCode.Core.Presentation;
using ICSharpCode.SharpDevelop;
using ICSharpCode.ShortcutsManagement.Data;
using Microsoft.Win32;
using ShortcutManagement=ICSharpCode.ShortcutsManagement.Data;
using SDCommandManager = ICSharpCode.Core.Presentation.CommandManager;
using MessageBox=System.Windows.MessageBox;
using Shortcut=ICSharpCode.ShortcutsManagement.Data.Shortcut;
using UserControl=System.Windows.Controls.UserControl;

namespace ICSharpCode.ShortcutsManagement.Dialogs
{
	/// <summary>
	/// Interaction logic for ShortcutsManagementOptionsPanel.xaml
	/// </summary>
	public partial class ShortcutsManagementOptionsPanel : UserControl, IOptionPanel
	{
		/// <summary>
		/// Identifies <see cref="SelectedProfile"/> dependency property
		/// </summary>
		public static readonly DependencyProperty SelectedProfileProperty = DependencyProperty.Register(
			"SelectedProfile",
			typeof(UserGestureProfile),
			typeof(ShortcutsManagementOptionsPanel),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
		
		/// <summary>
		/// Specifies text box border thickness
		/// </summary>
		public UserGestureProfile SelectedProfile
		{
			get {
				return (UserGestureProfile)GetValue(SelectedProfileProperty);
			}
			set {
				SetValue(SelectedProfileProperty, value);
			}
		}
		
		/// <summary>
		/// Stores shortcut entry to input binding convertion map
		/// </summary>
		private readonly MapTable<Shortcut, InputBindingInfo> shortcutsMap = new MapTable<Shortcut, InputBindingInfo>();
		
		private List<IShortcutTreeEntry> rootEntries;
		
		private readonly List<UserGestureProfile> profiles = new List<UserGestureProfile>();
		
		private readonly List<UserGestureProfile> removedProfiles = new List<UserGestureProfile>();
		
		public ShortcutsManagementOptionsPanel()
		{
			ResourceService.RegisterStrings("ICSharpCode.ShortcutsManagement.Resources.StringResources", GetType().Assembly);
			
			InitializeComponent();
		}
		
		public void LoadOptions()
		{
			shortcutsManagementOptionsPanel.searchTextBox.Focus();
			
			if (Directory.Exists(UserGestureManager.UserGestureProfilesDirectory)) {
				var dirInfo = new DirectoryInfo(UserGestureManager.UserGestureProfilesDirectory);
				var xmlFiles = dirInfo.GetFiles("*.xml");
				
				foreach (var fileInfo in xmlFiles) {
					var profile = new UserGestureProfile();
					profile.Path = Path.Combine(UserGestureManager.UserGestureProfilesDirectory, fileInfo.Name);
					profile.Load();
					profiles.Add(profile);
					
					if (UserGestureManager.CurrentProfile != null && profile.Name == UserGestureManager.CurrentProfile.Name) {
						SelectedProfile = profile;
					}
				}
			}
			
			BindProfiles();
			BindShortcuts();
		}
		
		private void BindProfiles()
		{
			var profilesTextBoxItemsSource = new ArrayList(profiles);
			
			profilesTextBoxItemsSource.Add(new SeparatorData());
			
			var deleteItem = new UserGestureProfileAction();
			deleteItem.Name = "Delete";
			deleteItem.Text = StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ProfileDeleteAction}");
			profilesTextBoxItemsSource.Add(deleteItem);
			
			var loadItem = new UserGestureProfileAction();
			loadItem.Name = "Load";
			loadItem.Text = StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ProfileLoadAction}");
			profilesTextBoxItemsSource.Add(loadItem);
			
			var createItem = new UserGestureProfileAction();
			createItem.Name = "Create";
			createItem.Text = StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ProfileCreateAction}");
			profilesTextBoxItemsSource.Add(createItem);
			
			var renameItem = new UserGestureProfileAction();
			renameItem.Name = "Rename";
			renameItem.Text = StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ProfileRenameAction}");
			profilesTextBoxItemsSource.Add(renameItem);
			
			var resetItem = new UserGestureProfileAction();
			resetItem.Name = "Reset";
			resetItem.Text = StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ProfileResetAction}");
			profilesTextBoxItemsSource.Add(resetItem);
			
			profilesTextBox.DataContext = profilesTextBoxItemsSource;
			
			if (SelectedProfile != null) {
				if(profilesTextBox.SelectedItem != SelectedProfile) {
					profilesTextBox.Text = SelectedProfile.Text;
					profilesTextBox.SelectedItem = SelectedProfile;
				}
			} else {
				profilesTextBox.SelectedIndex = -1;
				profilesTextBox.Text = "";
			}
			
		}
			
		private void BindShortcuts()
		{
			shortcutsMap.Clear();
			
			// Root shortcut tree entries
			rootEntries = new List<IShortcutTreeEntry>();
			
			// Stores SD input binding category to category section convertion map
			var categoriesMap = new MapTable<InputBindingCategory, ShortcutCategory>();
			
			var unspecifiedCategory = new ShortcutCategory(StringParser.Parse("${res:ShortcutsManagement.UnspecifiedCategoryName}"));
			rootEntries.Add(unspecifiedCategory);
			
			SDCommandManager.InputBindingCategories.Sort((c1, c2) => c1.Path.CompareTo(c2.Path));
			var parentCategories = new LinkedList<ShortcutCategory>();
			var previousDepth = 0;
			foreach(var bindingCategory in SDCommandManager.InputBindingCategories) {
				var categoryName = Regex.Replace(StringParser.Parse(bindingCategory.Text), @"&([^\s])", @"$1");
				var shortcutCategory = new ShortcutCategory(categoryName);
				categoriesMap.Add(bindingCategory, shortcutCategory);
			
			AddCategory:
				var currentDepth = bindingCategory.Path.Split('/').Length - 1;
				if (currentDepth > previousDepth)
				{
					previousDepth++;
					
					if (previousDepth > 1) {
						parentCategories.Last.Value.SubCategories.Add(shortcutCategory);
					} else {
						rootEntries.Add(shortcutCategory);
					}
					
					parentCategories.AddLast(shortcutCategory);
				} else {
					while (previousDepth >= currentDepth) {
						parentCategories.RemoveLast();
						previousDepth--;   
					}
				
					goto AddCategory;
				}
			}
			
			var inputBindingInfos = SDCommandManager.FindInputBindingInfos(new BindingInfoTemplate());
			foreach (var inputBindingInfo in inputBindingInfos) {
				// Get shortcut entry text. Normaly shortcut entry text is equal to routed command text
				// but this value can be overriden through InputBindingInfo.RoutedCommandText value
				var shortcutText = inputBindingInfo.RoutedCommand.Text;
				if (!string.IsNullOrEmpty(inputBindingInfo.RoutedCommandText)) {
					shortcutText = inputBindingInfo.RoutedCommandText;
				}
				
				shortcutText = StringParser.Parse(shortcutText);
				
				// Some commands have "&" sign to mark alternative key used to call this command from menu
				// Strip this sign from shortcut entry text
				shortcutText = Regex.Replace(shortcutText, @"&([^\s])", @"$1");
				
				var shortcutGestures = inputBindingInfo.DefaultGestures.InputGesturesCollection;
				if(SelectedProfile != null && SelectedProfile[BindingInfoTemplate.CreateFromIBindingInfo(inputBindingInfo)] != null) {
					shortcutGestures = new InputGestureCollection(SelectedProfile[BindingInfoTemplate.CreateFromIBindingInfo(inputBindingInfo)]);
				}
				
				var shortcut = new Shortcut(shortcutText, shortcutGestures);
				shortcutsMap.Add(shortcut, inputBindingInfo);
				
				// Assign shortcut to all categories it is registered in
				var isAnyCategoriesResolved = true;
				if (inputBindingInfo.Categories != null && inputBindingInfo.Categories.Count > 0) {
					foreach (var bindingCategory in inputBindingInfo.Categories) {
						ShortcutCategory shortcutCategory;
						categoriesMap.TryMapForward(bindingCategory, out shortcutCategory);
						if(shortcutCategory != null) {
							shortcutCategory.Shortcuts.Add(shortcut);
							isAnyCategoriesResolved = true;
						}
					}
				} 
				
				if(!isAnyCategoriesResolved) {
					rootEntries.Add(shortcut);
				}
			}
			
			rootEntries.Sort();
			
			foreach (var entry in rootEntries) {
				entry.SortSubEntries();
			}
			
			new ShortcutsFinder(rootEntries).Filter("");
			shortcutsManagementOptionsPanel.DataContext = rootEntries;
		}
		
		public bool SaveOptions() 
		{
			foreach (var profile in removedProfiles) {
				if(File.Exists(profile.Path)) {
					File.Delete(profile.Path);
				}
			}
			
			UserGestureManager.CurrentProfile = SelectedProfile;
			
			profiles.ForEach(p => p.Save());
			
			return true;
		}
		
		public object Owner 
		{
			get; set;
		}
		
		public object Control 
		{
			get { return this; }
		}
		
		private void profilesTextBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems == null || e.AddedItems.Count == 0) {
				return;
			}
			
			var userGestureProfileAction = e.AddedItems[0] as UserGestureProfileAction;
			if (userGestureProfileAction != null) {
				if (userGestureProfileAction.Name == "Delete" && SelectedProfile != null) {
					var result = MessageBox.Show(
					StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ConfirmDeleteProfileMessage}"),
					StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.ConfirmDeleteProfileMessageWindowName}"),
					MessageBoxButton.YesNo);
					
					if(MessageBoxResult.Yes == result) {
						profiles.Remove(SelectedProfile);
						removedProfiles.Add(SelectedProfile);
						SelectedProfile = null;
					}
				}
				
				if (userGestureProfileAction.Name == "Rename" && SelectedProfile != null) {
					var promptWindow = new CreateNewProfilePrompt();
					promptWindow.BaseProfilesVisibility = Visibility.Collapsed;
					promptWindow.Text = SelectedProfile.Text;
					promptWindow.ShowDialog();
					
					SelectedProfile.Text = promptWindow.Text;
				} 
				
				if(userGestureProfileAction.Name == "Load") {
					var openDialog = new OpenFileDialog();
					openDialog.Filter = "Xml files (*.xml)|*.xml";
					openDialog.FilterIndex = 1;
					openDialog.RestoreDirectory = false;
					if(true == openDialog.ShowDialog()) {
						var loadedProfile = new UserGestureProfile();
						loadedProfile.Path = openDialog.FileName;
						loadedProfile.Load();
						
						loadedProfile.Path = Path.Combine(
						UserGestureManager.UserGestureProfilesDirectory,
						Guid.NewGuid().ToString());
						
						profiles.Add(loadedProfile);
						SelectedProfile = loadedProfile;
					}
				}
				
				if (userGestureProfileAction.Name == "Reset") {
					SelectedProfile = null;
				}
				
				if(userGestureProfileAction.Name == "Create") {
					var promptWindow = new CreateNewProfilePrompt();
					promptWindow.AvailableBaseProfiles = profiles;
					promptWindow.ShowDialog();
					
					if (promptWindow.DialogResult == true) {
						UserGestureProfile newProfile;
						
						if (promptWindow.BaseProfile != null) {
						newProfile = (UserGestureProfile) promptWindow.BaseProfile.Clone();
							newProfile.Name = Guid.NewGuid().ToString();
							newProfile.Text = promptWindow.Text;
							newProfile.ReadOnly = false;
						} else {
							newProfile = new UserGestureProfile(Guid.NewGuid().ToString(), promptWindow.Text, false);
						}
						
						newProfile.Path = Path.Combine(UserGestureManager.UserGestureProfilesDirectory,
						newProfile.Name + ".xml");
						profiles.Add(newProfile);
						
						SelectedProfile = newProfile;
					}
				}
			}
			
			var userGestureProfile = e.AddedItems[0] as UserGestureProfile;
			if (userGestureProfile != null && userGestureProfile != SelectedProfile) {
				SelectedProfile = userGestureProfile;
			}
			
			BindProfiles(); 
			BindShortcuts();
		}
		
		private void shortcutsManagementOptionsPanel_ShortcutModified(object sender, EventArgs e)
		{
			var selectedShortcut = (Shortcut)shortcutsManagementOptionsPanel.shortcutsTreeView.SelectedItem;
			
			if(SelectedProfile == null)
			{
				MessageBox.Show(StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.NoProfileSelectedMessage}"));
				return;
			}
			
			if(SelectedProfile.ReadOnly)
			{
				MessageBox.Show(StringParser.Parse("${res:ShortcutsManagement.ShortcutsManagementOptionsPanel.SelectedProfileIsReadOnlyMessage}"));
				return;
			}
			
			Shortcut shortcutCopy = null;            
			ICollection<IShortcutTreeEntry> rootEntriesCopy = new ObservableCollection<IShortcutTreeEntry>();
			
			// Make a deep copy of all add-ins, categories and shortcuts
			foreach (var entry in rootEntries) {
				var clonedRootEntry = (IShortcutTreeEntry)entry.Clone();
				rootEntriesCopy.Add(clonedRootEntry);
				
				// Find copy of modified shortcut in copied add-ins collection
				if (shortcutCopy == null) {
					shortcutCopy = clonedRootEntry.FindShortcut(selectedShortcut.Id);
				}
			}
			
			var shortcutManagementWindow = new ShortcutManagementWindow(shortcutCopy, rootEntriesCopy);
			shortcutManagementWindow.ShowDialog();
			
			if (SelectedProfile != null && shortcutManagementWindow.DialogResult.HasValue && shortcutManagementWindow.DialogResult.Value) {
				// Move modifications from shortcut copies to original shortcut objects
				foreach (var relatedShortcutCopy in shortcutManagementWindow.ModifiedShortcuts) {
					foreach (var rootEntry in rootEntries) {
						var originalRelatedShortcut = rootEntry.FindShortcut(relatedShortcutCopy.Id);
						if (originalRelatedShortcut != null) {
							originalRelatedShortcut.Gestures.Clear();
							originalRelatedShortcut.Gestures.AddRange(relatedShortcutCopy.Gestures);
							
							var id = BindingInfoTemplate.CreateFromIBindingInfo(shortcutsMap.MapForward(originalRelatedShortcut));
							SelectedProfile[id] = new InputGestureCollection(relatedShortcutCopy.Gestures);
						}
					}
				}
			}
		}
	}
}
