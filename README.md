# Unity Autorun Tool



## 简介 / Summary

本程序可实现当在Unity编辑器中运行游戏时 (即`EditorApplication.isPlaying = true`), 按照你的设定, 自动地点击场景内的按钮, 帮助你完成一些项目启动时的初始化操作, 免去每次都需要手动操作的麻烦. 支持操作UGUI和FairyGUI两种主流UI框架的按钮!

This program can automatically click on the buttons in the scene according to your settings when running the game in the Unity editor (i.e. ` EditorApplication.isLayout = true `), helping you complete some initialization operations at project startup, eliminating the trouble of manual operations every time. Support operation of UGUI and FairyGUI buttons!



## 使用方法 / Guide

### 准备工作 / Prepare

1. 克隆项目到Unity工程中. 你可以将它放在`Asset`目录下的任意位置, Unity会自动寻找本项目中的`Editor`文件夹.

   Clone the project into the Unity project You can place it anywhere in the 'Asset' directory, and Unity will automatically search for the 'Editor' folder in this project

### 编辑配置 / Edit config

1. 点击编辑器顶部菜单栏的Window/AutoRunTool以打开GUI窗口.

   Click on Window/AutoRunTool in the top menu bar of the editor to open the GUI window

2. 初次使用时, 你应该根据按钮提示创建一个空的配置文件和配置预设.

   When using it for the first time, you should create an empty configuration file and configuration preset according to the button prompts.

3. 随后, 你可按照如下的说明进行配置:

   Subsequently, you can configure according to the following instructions:

```
config.xml
- 预设1
	- 步骤1
		- name: 要点击的按钮在Unity场景中的名字
		- text: 若同一界面中存在多个名称相同的按钮obj, 则读取它们的标题并用该字段筛选
		- delay: 执行点击操作**前**的延迟
		- FGUI: 是否是FairyGUI的按钮组件 (UGUI和FGUI按钮操作点击的方式不同)
		- 减号按钮: 删除当前步骤
	- 步骤2
		- ...
- 预设2
	- 步骤1
		- ...
```
```
config.xml
-Preset 1
	-Step 1
		-name: The name of the button to be clicked in the Unity scene
		-text: If there are multiple buttons with the same name obj in the same Unity scene, read their titles and use this field to filter them
		-delay: The delay **before** executing the click operation
		-FGUI: Is it a button component of FairyGUI? (UGUI and FGUI have different ways of operating and clicking buttons)
		-Minus button: Delete current step
	-Step 2
		- ...
-Preset 2
	-Step 1
		- ...
```
4. 完成配置后记得保存你的配置文件! 默认的保存位置在Unity编辑器的安装目录下(注意, 不是项目目录下).

   Remember to save your configuration file after completing the configuration! The default save location is in the installation directory of the Unity editor (note, not in the project directory)

5. 如果需要更改预设名称或配置内容, 可以直接打开配置文件的xml进行编辑. 我们提供了用于快速打开配置文件的`Open Config`按钮.

   If you need to change the preset name or configuration content, you can directly open the XML configuration file for editing We provide the 'Open Config' button for quickly opening configuration files


### 开始使用 / Go!

完成配置后你就可以开始使用了. 点击主面板上的`Go!`按钮以运行流程. 这个按钮会同时令编辑器开始播放, 所以你可以将它看做封装了自动化操作的播放按钮. 下方的`Stop`按钮同理.

程序通过在场景内创建自动处理器`AutoRunHandler`游戏对象来实现自动运行. 该对象不会随游戏结束而摧毁, 因此在第一次执行`Go!`后, 通过传统方式运行游戏同样会执行你设定的自动流程. 你可以点击`Clear`按钮来清理`AutoRunHandler`游戏对象 (*和控制台文本!*).

After completing the configuration, you can start using it Click on 'Go!' on the main panel` Button to run the process. This button will simultaneously start the editor to play, so you can consider it as a playback button that encapsulates automated operations. The 'Stop' button below is the same.

The program achieves automatic execution by creating an AutoRunHandler gameobject within the scene. This object will not be destroyed with the end of the game, so it will not be destroyed during the first execution of  'Go!'. Afterwards, running the game through traditional methods will also execute the automatic process you set. You can click the 'Clear' button to clear the 'AutoRunHandler' game objects (*and console text!*).
