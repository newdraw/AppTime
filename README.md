# 时间去哪儿了？
忙碌的一天匆匆而过，时间是如何分配的？是否需要管控？

# 试试这个吧
![](https://github.com/newdraw/AppTime/blob/master/files/tv.jpg) 

#### 功能：记录使用时间
每个应用和窗口的使用时间精确到秒。

![](https://github.com/newdraw/AppTime/blob/master/files/list.jpg)

#### 功能：回溯桌面
查看过往的桌面图像（可通过配置关闭）

![](https://github.com/newdraw/AppTime/blob/master/files/time.jpg)

![](https://github.com/newdraw/AppTime/blob/master/files/playback.gif)

#### 功能：分类管理
为应用或窗口加上标签，按类别统计。

![](https://github.com/newdraw/AppTime/blob/master/files/tag.jpg)
=>
![](https://github.com/newdraw/AppTime/blob/master/files/tagview.jpg)
 
# 使用方法
1、下载[Release.zip](https://github.com/newdraw/AppTime/raw/master/Release%20v0.11.zip)，解压到任意目录；

2、打开AppTime.exe;

3、注意右下角托盘图标。

![](https://github.com/newdraw/AppTime/blob/master/files/icon.jpg) 

# 升级注意
如需升级老版本的AppTime，请解压覆盖到老版本目录，但请不要覆盖data.db文件。

# 二次开发参考
开发环境Visual Studio 2019，.NET Framework 4.8。（其实不必须这么高版本，可以自行尝试降低）

发布时请把源码里的webui目录复制到exe所在目录。

# TODO List（欢迎朋友们一起完善）
1、支持多屏；

2、打开界面需要密码，截图、图标以及数据库用密码加密；

3、记录离开、屏保、锁屏等特殊情况的时间；

4、使用更现代的方式重构Web UI；

5、允许用户调整时间比例的分母为总时间或开机时间；

6、改进截图存取性能，延迟Windows关机直到截图保存完成；

7、预置常用标签，常见程序默认标签。

