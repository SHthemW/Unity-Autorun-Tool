# Unity Autorun Tool

[English version](#en)

## 简介 

本程序可实现当在Unity编辑器中运行游戏时 (即`EditorApplication.isPlaying = true`时), 按照你的设定, 自动地点击场景内的按钮, 帮助你完成一些项目启动时的初始化操作, 免去每次都需要手动操作的麻烦. 支持操作UGUI和FairyGUI两种主流UI框架的按钮!

## 使用方法

### 准备工作 

1. 克隆项目到Unity工程中. 你可以将它放在`Asset`目录下的任意位置.


### 编辑配置

1. 点击编辑器顶部菜单栏的Window/AutoRunTool以打开GUI窗口.

2. 初次使用时, 你应该根据UI界面上的提示, 点击按钮创建一个空的配置文件. 配置文件只能存在一个. (如果你的配置文件不为空, 创建配置文件的按钮将会被隐藏!)

3. 此时屏幕上会出现更多按钮. 点击"Save"按钮保存这个空的配置文件. 请务必先保存再执行后续步骤.

   配置文件默认的保存位置在Unity编辑器的安装目录下. (注意, 该目录不位于你的项目中, 不会对版本管理产生干扰)

4. 随后, 点击"创建预设"为你的配置文件创建一个自动运行预设(Preset). 你可以创建多个预设以便快速在不同的操作模式间切换.

   在创建第一个预设后, UI的样式会发生变化, 此后你可以点击预设下拉菜单右侧的"+"按钮来创建更多预设.

5. 在一个预设(Preset)中, 你可以创建两种行为: Action - Go 和 Action - Stop, 仅需点击对应名称右侧的"+"按钮. 这两种Action的数据结构和执行逻辑完全相同, 只是执行时机有区别:

   - Action - Go 会在点击Go按钮, 游戏运行后, 开始顺序执行. GoAction 一般用于游戏的初始化操作, 如进入场景后自动点击开始游戏按钮, 自动选择关卡等.
   - Action - Stop 会在点击Stop按钮后顺序执行, 直到执行完毕后才会停止游戏的运行. StopAction 一般用于游戏结束前自动退出房间等操作.

6. 行为具有一些属性, 以下是它们的注释:


```
- 预设(Preset)
|- 行为(Action)
 |- name: 要点击的按钮在Unity场景中的名字
 |- text: 若同一界面中存在多个名称相同的按钮obj, 则读取它们的标题并用该字段筛选. (FGUI按钮请将本字段保持默认)
 |- delay: 执行点击操作**前**的延迟
 |- FGUI: 是否是FairyGUI的按钮组件 (UGUI和FGUI按钮操作点击的方式不同)
 |- 减号按钮: 删除当前步骤
```
7. 完成配置后记得再次保存你的配置文件! 如果后续还需要更改预设名称或配置内容, 可以直接打开配置文件的xml进行编辑. 我们提供了用于快速打开配置文件的"Open Config"按钮.

### 开始使用

完成配置后你就可以开始使用了. 此后, 你可以当做面板上的 Go! 按钮和 Stop 按钮已经取代了你编辑器的播放和暂停按钮, 以进行封装后的开始/暂停游戏操作. 当然, 编辑器自己的播放和暂停按钮并未失效, 它们只是不会运行你自定义的 Action 而已.

程序是通过在场景内创建名为"AutoRunHandler"的游戏对象来实现自动运行的. 该对象不会随游戏结束而摧毁, 因此在第一次执行"Go!"后, 除非你手动移除场景里的"AutoRunHandler"物体, 否则后续点击Unity的开始游戏按钮也会执行Go-Action流程. 尽管如此, 我们仍建议你使用此工具面板上的按钮而非Unity自己的按钮运行游戏.

<h1 id="en"></h1>

# Unity Autorun Tool


## Introduction

This program can automatically click on the buttons in the scene according to your settings when running the game in the Unity editor (i.e. ` EditorApplication. isLayout=true `), helping you complete some initialization operations at project startup and avoiding the trouble of manual operations every time Support buttons for operating two mainstream UI frameworks, UGUI and FairyGUI!

## Tutorial

### Preparation work

1. Clone the project into the Unity project You can place it anywhere in the 'Asset' directory.

### Edit configuration

1. Click on Window/AutoRunTool in the top menu bar of the editor to open the GUI window.

2. When using it for the first time, you should follow the prompts on the UI interface and click the button to create an empty configuration file Only one configuration file can exist If your configuration file is not empty, the button to create the configuration file will be hidden.

3. At this point, more buttons will appear on the screen Click the "Save" button to save this empty configuration file Please make sure to save before proceeding with the next steps.

4. The default save location for configuration files is in the installation directory of the Unity editor (Note that this directory is not located in your project and will not interfere with version management).

5. Subsequently, click on "Create Preset" to create an auto run preset for your profile. You can create multiple presets to quickly switch between different operating modes.

6. After creating the first preset, The UI style will change, after which you can click the "+" button on the right side of the preset drop-down menu to create more presets.

7. In a preset, you can create two actions: Action Go and Action Stop, simply click the "+" button to the right of the corresponding name The data structure and execution logic of these two Actions are completely the same, except for the timing of execution:
   - Action-Go will start sequential execution after clicking the Go button. GoAction is generally used for game initialization operations, such as automatically clicking the start game button after entering the scene, automatically selecting levels, etc.
   - Action-Stop will be executed sequentially after clicking the Stop button, and the game will not stop running until it is completed. StopAction is generally used to automatically exit the room before the end of the game.

8. Action has some properties, and the following are their annotations:


```
- Preset
|- Action
 |- name: The name of the button to be clicked in the Unity scene
 |- text: If there are multiple buttons obj with the same name in the same interface, read their titles and filter them using this field (FGUI button, please keep this field as default)
 |- Delay: The delay before executing the click operation
 |- FGUI: Is it a button component of FairyGUI (UGUI and FGUI buttons operate and click differently)
 |- Minus button: Delete current step
```

9. Remember to save your configuration file again after completing the configuration! If you need to change the preset name or configuration content in the future, you can directly open the XML configuration file for editing We provide the "Open Config" button for quickly opening configuration files.

### Go!

After completing the configuration, you can start using it Afterwards. You can image that the Go! button and the Stop button have replaced the play and pause buttons in Unity editor for encapsulated start/pause game operations. Of course, the Unity's own play and pause buttons are not disabled, they just won't run your custom Action.

The program runs by creating a game object called "AutoRunHandler" within the scene. This object will not be destroyed with the end of the game, so after the first execution of "Go!", unless you manually remove the "AutoRunHandler" object in the scene, clicking the Unity start game button will also execute the Go Action process However, we still recommend that you use the buttons on this tool panel instead of Unity's own buttons to run the game.
