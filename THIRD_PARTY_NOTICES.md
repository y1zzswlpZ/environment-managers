# 第三方组件

JavaManager 和 PythonManager 的应用代码位于各自的 `source/` 目录；本说明描述随 PythonManager 分发的第三方 Python 组件，其许可仅适用于相应组件，不代表这些组件为本项目提供担保。

| 组件 | 版本 | 许可 |
| --- | --- | --- |
| virtualenv | 20.15.1 | MIT |
| distlib | 0.3.8 | Python Software Foundation License |
| filelock | 3.12.2 | Unlicense |
| platformdirs | 2.5.4 | MIT |
| six | 1.16.0 | MIT |

下载 URL、原始 wheel 文件名及校验值保存在 `PythonManager/tools/component-sources.json`。组件源码和许可文件保留在 `PythonManager/tools/legacy-host/` 对应目录。`virtualenv/seed/wheels/embed/` 内的 pip、setuptools、wheel 是该 virtualenv 发行版所附的离线创建组件，各自许可和元数据在 wheel 归档内保留。

未随包提供 Java / JDK / JRE、Python 解释器或 Windows / .NET 运行时。这些运行时需单独从各厂商获取并遵守其许可。Python 标识仅用于说明工具用途，本项目不是 Python Software Foundation 或 Oracle 的官方产品。
