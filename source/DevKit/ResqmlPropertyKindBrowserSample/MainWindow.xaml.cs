// 
// License notice
//  
// Standards DevKit, version 2.0
// Copyright 2012 ExxonMobil Upstream Research Company
// 
// Third Party Software
// 
// Energistics 
// The following Energistics (c) products were used in the creation of this work: 
// 
// •             WITSML Data Schema Specifications, Version 1.4.1.1 
// •             WITSML API Specifications, version 1.4.1.1 
// •             WITSML Data Schema Specifications, Version 1.3.1.1 
// •             WITSML API Specifications, version 1.3.1 
// •             PRODML Data Schema Specifications, Version 1.2.2 
// •             PRODML Web Service Specifications, Version 2.1.0.1
// •             RESQML Data Schema Specifications, Version 1.1 
// 
// All rights in the WITSML™ Standard, the PRODML™ Standard, and the RESQML™ Standard, or
// any portion thereof, which remain in the Standards DevKit shall remain with Energistics
// or its suppliers and shall remain subject to the terms of the Product License Agreement
// available at http://www.energistics.org/product-license-agreement. 
// 
// Apache
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. 
// 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. 
// 
// See the License for the specific language governing permissions and limitations under the
// License.
// 
// HDF5
// HDF5 (Hierarchical Data Format 5) Software Library and Utilities Copyright 2006-2012 by
// The HDF Group. 
// 
// NCSA HDF5 (Hierarchical Data Format 5) Software Library and Utilities Copyright 1998-2006
// by the Board of Trustees of the University of Illinois. 
// 
// All rights reserved. 
// 
// Redistribution and use in source and binary forms, with or without modification, are
// permitted for any purpose (including commercial purposes) provided that the following
// conditions are met: 
//    1. Redistributions of source code must retain the above copyright notice, this list
//       of conditions, and the following disclaimer. 
//    2. Redistributions in binary form must reproduce the above copyright notice, this
//       list of conditions, and the following disclaimer in the documentation and/or
// 	  materials provided with the distribution. 
//    3. In addition, redistributions of modified forms of the source or binary code must
//       carry prominent notices stating that the original code was changed and the date of
// 	  the change. 
//    4. All publications or advertising materials mentioning features or use of this
//       software are asked, but not required, to acknowledge that it was developed by The
// 	  HDF Group and by the National Center for Supercomputing Applications at the 
// 	  University of Illinois at Urbana-Champaign and credit the contributors. 
//    5. Neither the name of The HDF Group, the name of the University, nor the name of any
//       Contributor may be used to endorse or promote products derived from this software
// 	  without specific prior written permission from The HDF Group, the University, or
// 	  the Contributor, respectively. 
// 
// DISCLAIMER: THIS SOFTWARE IS PROVIDED BY THE HDF GROUP AND THE CONTRIBUTORS "AS IS" WITH
// NO WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED. In no event shall The HDF Group or
// the Contributors be liable for any damages suffered by the users arising out of the use
// of this software, even if advised of the possibility of such damage. 
// 
// Contributors: National Center for Supercomputing Applications (NCSA) at the University of
// Illinois, Fortner Software, Unidata Program Center (netCDF), The Independent JPEG Group
// (JPEG), Jean-loup Gailly and Mark Adler (gzip), and Digital Equipment Corporation (DEC). 
// 
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Energistics.DataAccess.RESQML110;

namespace ResqmlPropertyKindBrowserSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<ResqmlPropertyKind> found = new List<ResqmlPropertyKind>();
        public MainWindow()
        {
            InitializeComponent();
            
            List<ResqmlPropertyKind> all = ResqmlPropertyKind.GetList();

            ResqmlPropertyKind root = all.SingleOrDefault(pk => pk.Name.Equals("RESQML root property"));

            TreeViewItem item = new TreeViewItem() { Tag = root, Header = root.Name };
            tree.Items.Add(item);
            found.Add(root);
                        
            AddChildren(all, root, item);
                                    
            //Sanity Check... 
            if (all.Count != found.Count)
            {
                System.Windows.MessageBox.Show("Error, orphan found! all.Count = "+all.Count+" found.Count="+found.Count);
            }
        }

        

        private void AddChildren(List<ResqmlPropertyKind> all, ResqmlPropertyKind currentNode, TreeViewItem currentTreeNode)
        {
            //! [Linq]
            // Find all child ResqmlPropertyKind (child being defined as having a parent equal to the current node)
            IEnumerable<ResqmlPropertyKind> children = all.Where(pk => (pk.ParentKind != null && pk.ParentKind.Equals(currentNode.Name)));
            //! [Linq]

            foreach (var child in children)
            {
                TreeViewItem childTreeNode = new TreeViewItem() { Tag = child, Header = child.Name };
                currentTreeNode.Items.Add(childTreeNode);
                AddChildren(all, child, childTreeNode);
                found.Add(child);
            }
        }

        private void TreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem item = tree.SelectedItem as TreeViewItem;

            if (item != null)
            {
                propView.DataContext = item.Tag;
            }
        }

        
            
    }
}