# D寄存器位宽整理

本文档按当前源码中的读写方式整理 D 寄存器位宽，用于核对 PLC 读写长度，避免 16 位与 32 位混用。

## 判定规则

- 16 位：调用 `TryReadHoldingRegistersAsync(address, 1)` 或 `TryWriteSingleRegisterAsync(address, value)`，单个 D 寄存器保存一个 `ushort` 值。
- 32 位：连续两个 D 寄存器组成一个 `int`，低位在前，高位在后，即 `低16位地址=Dn`、`高16位地址=Dn+1`。
- 32 位组合方式：`raw = lowWord | (highWord << 16)`，按有符号 `Int32` 解释。
- 点位监控页面的 D 点位由 `PointMonitorConfig.json` 的 `RegisterBitWidth` 决定，未配置或非 32 时按 16 位处理。

## 16位D点

| 地址 | 用途 | 读写 | 代码依据 |
| --- | --- | --- | --- |
| D230 | 采血管数量同步 | 写 1 个寄存器 | `HomePlcGateway.SendTubeCountAsync` |
| D233-D254 | 首页料架工序状态监控区 | 读 22 个 16 位寄存器 | `HomePlcGateway.ReadRackProcessRegistersAsync`、`HomeRackProcessState` |
| D233 | 采血管运行槽位 | 读 | `TubeRunningRegisterOffsets` offset 0 |
| D234 | 采血管运行槽位，同时用于采血管摇匀当前生产号 | 读 | offset 1 |
| D235 | 顶空瓶运行槽位，同时用于顶空瓶摇匀当前生产号 | 读 | offset 2 |
| D236-D237 | 采血管运行槽位 | 读 | offsets 3、4 |
| D238 | 当前批量读取范围内，源码未解析使用 | 读 | D233-D254 批量读取 |
| D239-D243 | 顶空瓶运行槽位 | 读 | offsets 6-10 |
| D244 | 采血管运行槽位 | 读 | offset 11 |
| D245 | 采血管完成槽位 | 读 | offset 12 |
| D246-D250 | 顶空瓶运行槽位 | 读 | offsets 13-17 |
| D251 | 顶空瓶完成槽位 | 读 | offset 18 |
| D252 | 枪头已使用数量 | 读 | offset 19 |
| D253-D254 | 顶空瓶运行槽位 | 读 | offsets 20、21 |
| D6020 | 移液枪吸液延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6021 | 移液枪打液延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6022 | 采血管摇匀原位延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6023 | 采血管摇匀工位延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6024 | 采血管摇匀目标次数 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6026 | 顶空瓶摇匀原位延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6027 | 顶空瓶摇匀工位延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6028 | 顶空瓶摇匀目标次数 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6030 | 叔丁醇吸液延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6031 | 叔丁醇打液延时时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6040 | 样品瓶加压时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6041 | 定量环平衡时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D6042 | 进样时间 | 写/回读 1 个寄存器 | `BuildInitParameterItems` |
| D1028、D1128、D1228、D1328 | 各轴加速时间 | 写/读 1 个寄存器 | `AxisDebugAddressConfig`、`WriteAxisMotionTimeAsync` |
| D1029、D1129、D1229、D1329 | 各轴减速时间 | 写/读 1 个寄存器 | `AxisDebugAddressConfig`、`WriteAxisMotionTimeAsync` |
| 点位监控中 `RegisterBitWidth=16` 的 D 点 | 用户配置的 D 寄存器点位 | 读 1 个寄存器 | `PointMonitorViewModel.GetRegisterWordLength` |

## 32位D点

| 低位/高位地址 | 用途 | 读写 | 代码依据 |
| --- | --- | --- | --- |
| D6000/D6001 | Z轴丢枪头上升慢速速度 | 写 32 位并回读 2 个寄存器校验 | `HomePlcGateway.BuildInitParameterItems`、`ParameterConfigViewModel.WriteInt32ParameterAsync` |
| D1002/D1003 | M1 X轴当前位置 | 读 32 位 | `AxisDebugAddressConfig`、`RefreshAxisAsync` |
| D1004/D1005 | M1 X轴手动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1008/D1009 | M1 X轴自动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1016/D1017 | M1 X轴手动定位目标 | 读写 32 位 | `AxisDebugAddressConfig`、`ExecuteManualLocateAsync` |
| D1102/D1103 | M2 Y轴当前位置 | 读 32 位 | `AxisDebugAddressConfig`、`RefreshAxisAsync` |
| D1104/D1105 | M2 Y轴手动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1108/D1109 | M2 Y轴自动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1116/D1117 | M2 Y轴手动定位目标 | 读写 32 位 | `AxisDebugAddressConfig`、`ExecuteManualLocateAsync` |
| D1202/D1203 | M3 Z轴当前位置；重量转Z标定页默认读取当前Z | 读 32 位 | `AxisDebugAddressConfig`、`WeightToZDebugViewModel` |
| D1204/D1205 | M3 Z轴手动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1208/D1209 | M3 Z轴自动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1212/D1213 | 流程重量换算后的 Z 绝对位置下发 | 写 32 位 | `WorkflowSignalConfig.ZAbsolutePositionLowRegister`、`WorkflowEngine.WriteInt32AtAddressAsync` |
| D1216/D1217 | M3 Z轴手动定位目标 | 读写 32 位 | `AxisDebugAddressConfig`、`ExecuteManualLocateAsync` |
| D1302/D1303 | M4 摇匀轴当前位置 | 读 32 位 | `AxisDebugAddressConfig`、`RefreshAxisAsync` |
| D1304/D1305 | M4 摇匀轴手动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1308/D1309 | M4 摇匀轴自动速度 | 读写 32 位 | `AxisDebugAddressConfig`、`WriteAxisSpeedAsync` |
| D1316/D1317 | M4 摇匀轴手动定位目标 | 读写 32 位 | `AxisDebugAddressConfig`、`ExecuteManualLocateAsync` |
| D6302/D6303 | 样品瓶加压位置 | 写 32 位并回读 2 个寄存器校验 | `HomePlcGateway.BuildInitParameterItems`、`ParameterConfigViewModel.WriteInt32ParameterAsync` |
| D6304/D6305 | 定量环平衡位置 | 写 32 位并回读 2 个寄存器校验 | `HomePlcGateway.BuildInitParameterItems`、`ParameterConfigViewModel.WriteInt32ParameterAsync` |
| D6306/D6307 | 进样位置 | 写 32 位并回读 2 个寄存器校验 | `HomePlcGateway.BuildInitParameterItems`、`ParameterConfigViewModel.WriteInt32ParameterAsync` |
| 点位监控中 `RegisterBitWidth=32` 的 D 点 | 用户配置的 D 寄存器点位 | 从低位地址开始读 2 个寄存器 | `PointMonitorViewModel.GetRegisterWordLength` |

## 坐标调试默认32位地址区

坐标调试页的 XY/Z 点表均按 32 位写入，每个点占连续两个 D 寄存器，默认步长为 2。以下为源码默认配置；如果运行目录存在 `CoordinateDebugConfig.json`，实际地址以该配置文件为准。

| 模块 | 点数 | 默认低位地址规律 | 实际占用范围 | 说明 |
| --- | ---: | --- | --- | --- |
| 采血管 X | 50 | D5100 + 点序号偏移 * 2 | D5100-D5199 | 每点 X 为 32 位 |
| 采血管 Y | 50 | D5200 + 点序号偏移 * 2 | D5200-D5299 | 每点 Y 为 32 位 |
| 顶空瓶 X | 100 | D5300 + 点序号偏移 * 2 | D5300-D5499 | 每点 X 为 32 位 |
| 顶空瓶 Y | 100 | D5500 + 点序号偏移 * 2 | D5500-D5699 | 每点 Y 为 32 位 |
| 其他工位 X | 50 | D5700 + 点序号偏移 * 2 | D5700-D5799 | 每点 X 为 32 位 |
| 其他工位 Y | 50 | D5800 + 点序号偏移 * 2 | D5800-D5899 | 每点 Y 为 32 位 |
| Z轴点表 | 18 | D5900 + 点序号偏移 * 2 | D5900-D5935 | 每点 Z 为 32 位 |
| 枪头 X | 50 | D6100 + 点序号偏移 * 2 | D6100-D6199 | 每点 X 为 32 位 |
| 枪头 Y | 50 | D6200 + 点序号偏移 * 2 | D6200-D6299 | 每点 Y 为 32 位 |

## 当前需要重点确认

- `D6000/D6001`、`D6302/D6303`、`D6304/D6305`、`D6306/D6307` 当前已按 32 位低高字写入；PLC 侧也需要按相同低高字顺序解析。
- `D1212/D1213` 是流程中重量转 Z 的 32 位下发地址，和轴当前位置 `D1202/D1203` 不是同一组地址。
- `Config/AxisDebugAddressConfig.json` 当前只显式保存了部分轴寄存器字段；代码会为缺失的速度高位、加减速时间字段补默认值并保存。核对现场配置时要以运行目录 `Config/AxisDebugAddressConfig.json` 的最终内容为准。
- 坐标调试地址区可由运行目录 `CoordinateDebugConfig.json` 改写，不能只看源码默认范围。
- 点位监控页自身不写 D 寄存器，只读 D 寄存器；D 点位的 16/32 位来自 `RegisterBitWidth` 字段。
