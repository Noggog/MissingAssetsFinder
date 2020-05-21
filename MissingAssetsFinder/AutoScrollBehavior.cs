﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

/*
 * Source: https://github.com/wabbajack-tools/wabbajack/blob/master/Wabbajack/Util/AutoScrollBehavior.cs
 */

namespace MissingAssetsFinder
{
    internal class AutoScrollBehavior
    {
        private static readonly Dictionary<ListBox, Capture> Associations =
            new Dictionary<ListBox, Capture>();

        public static readonly DependencyProperty ScrollOnNewItemProperty =
            DependencyProperty.RegisterAttached(
                "ScrollOnNewItem",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new UIPropertyMetadata(false, OnScrollOnNewItemChanged));

        public static bool GetScrollOnNewItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollOnNewItemProperty);
        }

        public static void SetScrollOnNewItem(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollOnNewItemProperty, value);
        }

        public static void OnScrollOnNewItemChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ListBox listBox)) return;
            bool oldValue = (bool)e.OldValue, newValue = (bool)e.NewValue;
            if (newValue == oldValue) return;
            if (newValue)
            {
                listBox.Loaded += ListBox_Loaded;
                listBox.Unloaded += ListBox_Unloaded;
                var itemsSourcePropertyDescriptor = TypeDescriptor.GetProperties(listBox)["ItemsSource"];
                itemsSourcePropertyDescriptor.AddValueChanged(listBox, ListBox_ItemsSourceChanged!);
            }
            else
            {
                listBox.Loaded -= ListBox_Loaded;
                listBox.Unloaded -= ListBox_Unloaded;
                if (Associations.ContainsKey(listBox))
                    Associations[listBox].Dispose();
                var itemsSourcePropertyDescriptor = TypeDescriptor.GetProperties(listBox)["ItemsSource"];
                itemsSourcePropertyDescriptor.RemoveValueChanged(listBox, ListBox_ItemsSourceChanged!);
            }
        }

        private static void ListBox_ItemsSourceChanged(object sender, EventArgs e)
        {
            var listBox = (ListBox)sender;
            if (Associations.ContainsKey(listBox))
                Associations[listBox].Dispose();
            Associations[listBox] = new Capture(listBox);
        }

        private static void ListBox_Unloaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (Associations.ContainsKey(listBox))
                Associations[listBox].Dispose();
            listBox.Unloaded -= ListBox_Unloaded;
        }

        private static void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.Items == null) return;
            listBox.Loaded -= ListBox_Loaded;
            Associations[listBox] = new Capture(listBox);
        }

        private class Capture : IDisposable
        {
            private readonly INotifyCollectionChanged _incc;
            private readonly ListBox _listBox;

            public Capture(ListBox listBox)
            {
                _listBox = listBox;
                _incc = (listBox.ItemsSource as INotifyCollectionChanged)!;
                if (_incc != null) _incc.CollectionChanged += incc_CollectionChanged;
            }

            public void Dispose()
            {
                if (_incc != null)
                    _incc.CollectionChanged -= incc_CollectionChanged;
            }

            private void incc_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action != NotifyCollectionChangedAction.Add) return;
                try
                {
                    _listBox.ScrollIntoView(e.NewItems[0]);
                    _listBox.SelectedItem = e.NewItems[0];
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }
    }
}
