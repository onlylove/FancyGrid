﻿using Microsoft.Windows.Controls;
using Microsoft.Windows.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Linq;
using System.Windows.Input;

namespace Labs.Filtering
{
    /// <summary>
    /// A grid that makes inline filtering possible.
    /// </summary>
    public class FilteringDataGrid : DataGrid
    {
        /// <summary>
        /// This dictionary will have a list of all applied filters
        /// </summary>
        private Dictionary<string, string> columnFilters;

        /// <summary>
        /// Cache with properties for better performance
        /// </summary>
        private Dictionary<string, PropertyInfo> propertyCache;

        /// <summary>
        /// Case sensitive filtering
        /// </summary>
        public static DependencyProperty IsFilteringCaseSensitiveProperty =
             DependencyProperty.Register("IsFilteringCaseSensitive", typeof(bool), typeof(FilteringDataGrid), new PropertyMetadata(true));

        /// <summary>
        /// Case sensitive filtering
        /// </summary>
        public bool IsFilteringCaseSensitive
        {
            get { return (bool)(GetValue(IsFilteringCaseSensitiveProperty)); }
            set { SetValue(IsFilteringCaseSensitiveProperty, value); }
        }

        public IEnumerable<object> FilteredItems
        {
            get
            { //TODO Better way to do this
                List<object> fitems = new List<object>();
                foreach (var item in ItemsSource)
                {
                    if (Filter(item))
                        fitems.Add(item);
                }
                return fitems;
            }
        }

        /// <summary>
        /// Register for all text changed events
        /// </summary>
        public FilteringDataGrid()
        {
            // Initialize lists
            columnFilters = new Dictionary<string, string>();
            propertyCache = new Dictionary<string, PropertyInfo>();

            // Add a handler for all text changes
            AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(OnTextChanged), true);

            // Datacontext changed, so clear the cache
            DataContextChanged += new DependencyPropertyChangedEventHandler(FilteringDataGrid_DataContextChanged);

            // To enable multisort
            //CanUserSortColumns = false;
            //MouseLeftButtonUp += FilteringDataGrid_MouseLeftButtonUp;
            //Sorting += DataGrid_Standard_Sorting;
            Sorting += FilteringDataGrid_Sorting;
        }

        void FilteringDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(Items);

            foreach (var column in Columns)
            {
             
                 var sd = Helpers.FindSortDescription(view.SortDescriptions, column.SortMemberPath);
                 if (sd.HasValue)
                     column.SortDirection = sd.Value.Direction;
            }

            if (e.Column.SortDirection.HasValue)
            {

                view.SortDescriptions.Remove(view.SortDescriptions.FirstOrDefault(sd => sd.PropertyName == e.Column.Header.ToString()));
                switch (e.Column.SortDirection.Value)
                {
                    case ListSortDirection.Ascending:
                        e.Column.SortDirection = ListSortDirection.Descending;
                        view.SortDescriptions.Add(new SortDescription(e.Column.Header.ToString(), ListSortDirection.Descending));
                        break;
                    case ListSortDirection.Descending:
                        e.Column.SortDirection = null;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                e.Column.SortDirection = ListSortDirection.Ascending;
                view.SortDescriptions.Add(new SortDescription(e.Column.Header.ToString(), ListSortDirection.Ascending));
            }
            e.Handled = true; ;
        }

        /// <summary>
        /// Clear the property cache if the datacontext changes.
        /// This could indicate that an other type of object is bound.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FilteringDataGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            propertyCache.Clear();
        }

        /// <summary>
        /// When a text changes, it might be required to filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Get the textbox
            TextBox filterTextBox = e.OriginalSource as TextBox;

            // Get the header of the textbox
            DataGridColumnHeader header = TryFindParent<DataGridColumnHeader>(filterTextBox);
            if (header != null)
            {
                UpdateFilter(filterTextBox, header);
                ApplyFilters();
            }
        }

        /// <summary>
        /// Update the internal filter
        /// </summary>
        /// <param name="textBox"></param>
        /// <param name="header"></param>
        private void UpdateFilter(TextBox textBox, DataGridColumnHeader header)
        {
            // Try to get the property bound to the column.
            // This should be stored as datacontext.
            string columnBinding = header.DataContext != null ? header.DataContext.ToString() : "";

            // Set the filter 
            if (!String.IsNullOrEmpty(columnBinding))
                columnFilters[columnBinding] = textBox.Text;
        }

        /// <summary>
        /// Apply the filters
        /// </summary>
        private void ApplyFilters()
        {
            // Get the view
            ICollectionView view = CollectionViewSource.GetDefaultView(ItemsSource);
            if (view != null)
            {
                view.Filter = Filter;
            }
        }


        /// <summary>
        /// The logic for filtering
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private bool Filter(object item)
        {
            // Show the current object
            bool show = true;

            // Loop filters
            foreach (KeyValuePair<string, string> filter in columnFilters)
            {
                object property = GetPropertyValue(item, filter.Key);
                if (property != null)
                {
                    // Check if the current column contains a filter
                    bool containsFilter = false;
                    if (IsFilteringCaseSensitive)
                        containsFilter = property.ToString().Contains(filter.Value);
                    else
                        containsFilter = property.ToString().ToLower().Contains(filter.Value.ToLower());

                    // Do the necessary things if the filter is not correct
                    if (!containsFilter)
                    {
                        show = false;
                        break;
                    }
                }
            }

            // Return if it's visible or not
            return show;
        }

        /// <summary>
        /// Get the value of a property
        /// </summary>
        /// <param name="item"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private object GetPropertyValue(object item, string property)
        {
            // No value
            object value = null;

            // Get property  from cache
            PropertyInfo pi = null;
            if (propertyCache.ContainsKey(property))
                pi = propertyCache[property];
            else
            {
                pi = item.GetType().GetProperty(property);
                propertyCache.Add(property, pi);
            }

            // If we have a valid property, get the value
            if (pi != null)
                value = pi.GetValue(item, null);

            // Done
            return value;
        }

        /// <summary>
        /// Finds a parent of a given item on the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of the queried item.</typeparam>
        /// <param name="child">A direct or indirect child of the queried item.</param>
        /// <returns>The first parent item that matches the submitted
        /// type parameter. If not matching item can be found, a null reference is being returned.</returns>
        public static T TryFindParent<T>(DependencyObject child)
          where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = GetParentObject(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                //use recursion to proceed with next level
                return TryFindParent<T>(parentObject);
            }
        }

        /// <summary>
        /// This method is an alternative to WPF's
        /// <see cref="VisualTreeHelper.GetParent"/> method, which also
        /// supports content elements. Do note, that for content element,
        /// this method falls back to the logical tree of the element.
        /// </summary>
        /// <param name="child">The item to be processed.</param>
        /// <returns>The submitted item's parent, if available. Otherwise null.</returns>
        public static DependencyObject GetParentObject(DependencyObject child)
        {
            if (child == null) return null;
            ContentElement contentElement = child as ContentElement;

            if (contentElement != null)
            {
                DependencyObject parent = ContentOperations.GetParent(contentElement);
                if (parent != null) return parent;

                FrameworkContentElement fce = contentElement as FrameworkContentElement;
                return fce != null ? fce.Parent : null;
            }

            // If it's not a ContentElement, rely on VisualTreeHelper
            return VisualTreeHelper.GetParent(child);
        }
    }
}