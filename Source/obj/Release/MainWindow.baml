   M S B A M L   `   `   `  ���� D  @RedditApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null%clr-namespace:RedditApp	RedditApp      RedditApp.MainWindow   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     )localclr-namespace:RedditApp     -     � Title$  reddit images��$	��600��$
��1000�� � Icon$ snoo.png��.���  � ColumnDefinitions �� $
��auto ��� -    ��Name$ CenterGridColumn��.+�� $��0�� � RowDefinitions �� �� $
��auto ��� $
��auto �.+��� �� -    ��Name$ ListboxOfSubreddits��$��0��$��0������ *SubredditList+ �� UpdateSourceTrigger$ PropertyChanged=����� .R��� .+��� -    d� 	IsChecked �� *
Selected+���� *Label+�� $��2��.+���  �� Orientation$ 
Horizontal=�.+��� .��Posts from each:�� -   	 ��Name$	 PostCountTextBox��$��40��$��Center=����� *	PostCount+$ PropertyChanged=��� -   
 ��Name$
 RefreshButton��$��2,2,2,2q�.��	Refresh�� .��Manage Subreddits�� -   $	 AddSRTextbox��$��Center=�$	��150���� -   $
 AddSRButton��$��2,2,2,2q�.��Add�� -	   $
 RemoveSRButton��$��2,2,2,2q�.��Remove Selected�� -
    ��Name$ canvas2��$��0��$	��100��$	��100��$y�	Collapsed=�$��0,0,0,10q�$��Bottom=�.+��� -   $ canvas1��$��0��$	��100��$	��100�� J� RenderTransformOrigin$ .5,.5&�z��� -   spin�� �� Angle$ 0Q��� -    ��Name$ 
CanvasText��$��1��$
��auto�� �� Left$ 20��
 �� Top$ 36��$
��auto��.��
Loading...� $��1�� �� �� $
��auto ��� $
��auto �.+��� -    ��Name$ ListboxOfPhotos��$��0��$��0��$��Disabled=����� *
Thumbnails+���� .R��� $��1q�����  �� Path$ Image�����  �� ElementName$ 
ZoomSlider��$ Value� J� Effect 3  -System.Windows.Media.Effects.DropShadowEffect     	Direction$	 325Q�   Color$ #555555��   ShadowDepth$ 7Q�   
BlurRadius$ 3Q����� .R��  � Orientation$ 
Horizontal=��� $��2��$ 
Horizontal=�$
��Left=�$
��auto��.+��� -   $
 
SaveButton��$��1��$��3,0,3,0q�.��Save These Images�� -   $
 ClearButton��$��3,0,3,0q�.��Clear Messages�� -   $
 
DemoButton��$��Right=�$��3,0,3,0q�.��Demo�� -    ��Name$ SFWFilterBox��$	��100��$��3,0,3,0q� �� SelectedIndex$ 0��.@��� .��Safe for Work�� .��All Images (No filter)�� .��	Only NSFW�� -   $ ReportingFilterBox��$	��100��$��3,0,3,0q�$ 0��.@��� .��Report Errors�� .��Verbose Mode�� .��Silent Mode�� -    ��Name$ 
ZoomSlider�� � Value$	 200Q� � Minimum$ 80Q� � Maximum$	 550Q�  �� Orientation$  
Horizontal=�$	��200��$��0,0,0,0q�$��Center=��� $��Center=�.��Zoom�� -   $ ListboxOfMessages��$��1��$��1������ *MessageList+���� .R��� .+��  �� $	��150 ��� .+��� $��0��! �� TextAlignment$
! Left=����� $ Source��� $��1������ $ Message�