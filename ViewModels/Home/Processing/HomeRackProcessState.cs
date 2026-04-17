using System;
using System.Collections.Generic;
using System.Globalization;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页料架工序状态机。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 负责解析 D233~D254 工序寄存器，维护料架槽位集合、枪头使用数和摇晃生产号变化。
/// </remarks>
internal sealed class HomeRackProcessState
{
	private static readonly int[] TubeRunningRegisterOffsets = new int[5] { 0, 1, 3, 4, 11 };
	private static readonly int[] TubeCompletedRegisterOffsets = new int[1] { 12 };
	private static readonly int[] HeadspaceRunningRegisterOffsets = new int[13]
	{
		2, 6, 7, 8, 9, 10, 13, 14, 15, 16,
		17, 20, 21
	};

	private static readonly int[] HeadspaceCompletedRegisterOffsets = new int[1] { 18 };

	private const int NeedleHeadUsedCountRegisterOffset = 19;

	private const int TubeShakeCurrentProductionRegisterOffset = 1;

	private const int HeadspaceShakeCurrentProductionRegisterOffset = 2;

	private readonly int _maxTubeCount;
	private readonly int _maxHeadspaceCount;
	private readonly int _maxNeedleHeadCount;
	private readonly HashSet<int> _tubeRunningSlots = new HashSet<int>();
	private readonly HashSet<int> _tubeCompletedSlots = new HashSet<int>();
	private readonly HashSet<int> _headspaceRunningSlots = new HashSet<int>();
	private readonly HashSet<int> _headspaceCompletedSlots = new HashSet<int>();
	private int _usedNeedleHeadCount;
	private int _lastTubeShakeProductionNumber;
	private int _lastHeadspaceShakeProductionNumber;

	/// <summary>
	/// 初始化首页料架工序状态机。
	/// </summary>
	/// By:ChengLei
	/// <param name="maxTubeCount">采血管最大槽位数。</param>
	/// <param name="maxHeadspaceCount">顶空瓶最大槽位数。</param>
	/// <param name="maxNeedleHeadCount">针头最大槽位数。</param>
	/// <remarks>
	/// 由 HomeViewModel 构造时创建，用于隔离料架寄存器解析细节。
	/// </remarks>
	public HomeRackProcessState(int maxTubeCount, int maxHeadspaceCount, int maxNeedleHeadCount)
	{
		_maxTubeCount = maxTubeCount;
		_maxHeadspaceCount = maxHeadspaceCount;
		_maxNeedleHeadCount = maxNeedleHeadCount;
	}

	/// <summary>
	/// 采血管运行中槽位集合。
	/// </summary>
	/// By:ChengLei
	public IReadOnlySet<int> TubeRunningSlots => _tubeRunningSlots;

	/// <summary>
	/// 采血管已完成槽位集合。
	/// </summary>
	/// By:ChengLei
	public IReadOnlySet<int> TubeCompletedSlots => _tubeCompletedSlots;

	/// <summary>
	/// 顶空瓶运行中槽位集合。
	/// </summary>
	/// By:ChengLei
	public IReadOnlySet<int> HeadspaceRunningSlots => _headspaceRunningSlots;

	/// <summary>
	/// 顶空瓶已完成槽位集合。
	/// </summary>
	/// By:ChengLei
	public IReadOnlySet<int> HeadspaceCompletedSlots => _headspaceCompletedSlots;

	/// <summary>
	/// 当前已使用针头数量。
	/// </summary>
	/// By:ChengLei
	public int UsedNeedleHeadCount => _usedNeedleHeadCount;

	/// <summary>
	/// 应用料架工序寄存器并返回解析结果。
	/// </summary>
	/// By:ChengLei
	/// <param name="registers">D233~D254读取结果。</param>
	/// <param name="isDetectionStarted">检测流程是否已启动。</param>
	/// <param name="batchNo">当前批次号。</param>
	/// <returns>返回料架解析结果。</returns>
	/// <remarks>
	/// 由 HomeViewModel 在 UI 线程调用，解析后的事件仍由原采血管事件队列串行处理。
	/// </remarks>
	public HomeRackProcessResult ApplyRegisters(IReadOnlyList<ushort> registers, bool isDetectionStarted, string batchNo)
	{
		HashSet<int> tubeRunning = ExtractSlotsFromRegisters(registers, TubeRunningRegisterOffsets, _maxTubeCount);
		HashSet<int> tubeCompleted = ExtractSlotsFromRegisters(registers, TubeCompletedRegisterOffsets, _maxTubeCount);
		HashSet<int> headspaceRunning = ExtractSlotsFromRegisters(registers, HeadspaceRunningRegisterOffsets, _maxHeadspaceCount);
		HashSet<int> headspaceCompleted = ExtractSlotsFromRegisters(registers, HeadspaceCompletedRegisterOffsets, _maxHeadspaceCount);
		int usedNeedleHeadCount = ExtractNeedleHeadUsedCount(registers);
		int tubeShakeProductionNumber = ExtractProductionNumber(registers, TubeShakeCurrentProductionRegisterOffset, _maxTubeCount);
		int headspaceShakeProductionNumber = ExtractProductionNumber(registers, HeadspaceShakeCurrentProductionRegisterOffset, _maxHeadspaceCount);

		List<TubeProcessEvent> events = BuildShakeProductionEvents(tubeShakeProductionNumber, headspaceShakeProductionNumber, isDetectionStarted, batchNo);

		bool changed = ReplaceSlotSet(_tubeRunningSlots, tubeRunning);
		changed |= ReplaceSlotSet(_tubeCompletedSlots, tubeCompleted);
		changed |= ReplaceSlotSet(_headspaceRunningSlots, headspaceRunning);
		changed |= ReplaceSlotSet(_headspaceCompletedSlots, headspaceCompleted);
		changed |= UpdateNeedleHeadUsedCount(usedNeedleHeadCount);

		return new HomeRackProcessResult(changed, events);
	}

	/// <summary>
	/// 清空料架工序状态。
	/// </summary>
	/// By:ChengLei
	/// <returns>返回状态是否发生变化。</returns>
	/// <remarks>
	/// 由 PLC 离线、检测停止和急停流程调用，避免显示旧工序状态。
	/// </remarks>
	public bool Clear()
	{
		if (_tubeRunningSlots.Count == 0 &&
			_tubeCompletedSlots.Count == 0 &&
			_headspaceRunningSlots.Count == 0 &&
			_headspaceCompletedSlots.Count == 0 &&
			_usedNeedleHeadCount == 0 &&
			_lastTubeShakeProductionNumber == 0 &&
			_lastHeadspaceShakeProductionNumber == 0)
		{
			return false;
		}

		_tubeRunningSlots.Clear();
		_tubeCompletedSlots.Clear();
		_headspaceRunningSlots.Clear();
		_headspaceCompletedSlots.Clear();
		_usedNeedleHeadCount = 0;
		_lastTubeShakeProductionNumber = 0;
		_lastHeadspaceShakeProductionNumber = 0;
		return true;
	}

	/// <summary>
	/// 从寄存器集合按偏移提取有效槽位编号。
	/// </summary>
	/// By:ChengLei
	/// <param name="registers">读取到的寄存器集合。</param>
	/// <param name="offsets">需要提取的偏移集合。</param>
	/// <param name="maxSlotNumber">槽位最大编号限制。</param>
	/// <returns>返回提取到的槽位编号集合。</returns>
	/// <remarks>
	/// 由 ApplyRegisters 分别提取采血管和顶空瓶工序槽位时调用。
	/// </remarks>
	private static HashSet<int> ExtractSlotsFromRegisters(IReadOnlyList<ushort> registers, IEnumerable<int> offsets, int maxSlotNumber)
	{
		HashSet<int> slots = new HashSet<int>();
		foreach (int offset in offsets)
		{
			if (offset >= 0 && offset < registers.Count)
			{
				int slotNumber = registers[offset];
				if (slotNumber > 0 && slotNumber <= maxSlotNumber)
				{
					slots.Add(slotNumber);
				}
			}
		}

		return slots;
	}

	/// <summary>
	/// 从寄存器集合提取当前已使用的移液枪头数量。
	/// </summary>
	/// By:ChengLei
	/// <param name="registers">读取到的寄存器集合。</param>
	/// <returns>返回已使用枪头数量。</returns>
	/// <remarks>
	/// 由 ApplyRegisters 调用，对应 D252 的料架枪头当前生产号码。
	/// </remarks>
	private int ExtractNeedleHeadUsedCount(IReadOnlyList<ushort> registers)
	{
		if (NeedleHeadUsedCountRegisterOffset < 0 || NeedleHeadUsedCountRegisterOffset >= registers.Count)
		{
			return 0;
		}

		return Math.Clamp((int)registers[NeedleHeadUsedCountRegisterOffset], 0, _maxNeedleHeadCount);
	}

	/// <summary>
	/// 从寄存器集合提取指定工序的当前生产号。
	/// </summary>
	/// By:ChengLei
	/// <param name="registers">读取到的寄存器集合。</param>
	/// <param name="offset">对应 D233 起始区间的偏移。</param>
	/// <param name="maxNumber">允许的最大生产号。</param>
	/// <returns>返回归一化后的当前生产号，无效时返回 0。</returns>
	/// <remarks>
	/// 由 ApplyRegisters 在摇晃工序日志提取时调用。
	/// </remarks>
	private static int ExtractProductionNumber(IReadOnlyList<ushort> registers, int offset, int maxNumber)
	{
		if (offset < 0 || offset >= registers.Count)
		{
			return 0;
		}

		return Math.Clamp((int)registers[offset], 0, maxNumber);
	}

	/// <summary>
	/// 根据摇晃当前生产号变化构建流程事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="tubeShakeProductionNumber">采血管摇晃当前生产号。</param>
	/// <param name="headspaceShakeProductionNumber">顶空瓶摇晃当前生产号。</param>
	/// <param name="isDetectionStarted">检测流程是否已启动。</param>
	/// <param name="batchNo">当前批次号。</param>
	/// <returns>返回需要入队处理的流程事件集合。</returns>
	/// <remarks>
	/// 由 ApplyRegisters 调用，仅在编号变化且检测已启动时生成事件。
	/// </remarks>
	private List<TubeProcessEvent> BuildShakeProductionEvents(
		int tubeShakeProductionNumber,
		int headspaceShakeProductionNumber,
		bool isDetectionStarted,
		string batchNo)
	{
		List<TubeProcessEvent> events = new List<TubeProcessEvent>();
		AddTubeShakeProductionEvent(events, tubeShakeProductionNumber, isDetectionStarted, batchNo);
		AddHeadspaceShakeProductionEvent(events, headspaceShakeProductionNumber, isDetectionStarted, batchNo);
		return events;
	}

	/// <summary>
	/// 根据采血管摇晃当前生产号变化追加事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="events">目标事件集合。</param>
	/// <param name="tubeShakeProductionNumber">采血管摇晃当前生产号。</param>
	/// <param name="isDetectionStarted">检测流程是否已启动。</param>
	/// <param name="batchNo">当前批次号。</param>
	/// <remarks>
	/// 由 BuildShakeProductionEvents 调用，变化为正数时写入对应管号日志事件。
	/// </remarks>
	private void AddTubeShakeProductionEvent(
		List<TubeProcessEvent> events,
		int tubeShakeProductionNumber,
		bool isDetectionStarted,
		string batchNo)
	{
		if (_lastTubeShakeProductionNumber == tubeShakeProductionNumber)
		{
			return;
		}

		_lastTubeShakeProductionNumber = tubeShakeProductionNumber;
		if (!isDetectionStarted || tubeShakeProductionNumber <= 0)
		{
			return;
		}

		string message = $"采血管摇晃当前生产号={tubeShakeProductionNumber}。";
		events.Add(new TubeProcessEvent
		{
			Timestamp = DateTime.Now,
			BatchNo = batchNo,
			TubeIndex = tubeShakeProductionNumber,
			ProcessName = "采血管摇匀",
			EventName = "摇晃编号",
			PlcValue = tubeShakeProductionNumber.ToString(CultureInfo.InvariantCulture),
			Note = message,
			HomeLogLevel = HomeLogLevel.Info,
			HomeLogSource = HomeLogSource.Process,
			HomeLogKind = HomeLogKind.Detection,
			HomeLogMessage = message,
			PersistHomeLogToFile = true
		});
	}

	/// <summary>
	/// 根据顶空瓶摇晃当前生产号变化追加事件。
	/// </summary>
	/// By:ChengLei
	/// <param name="events">目标事件集合。</param>
	/// <param name="headspaceShakeProductionNumber">顶空瓶摇晃当前生产号。</param>
	/// <param name="isDetectionStarted">检测流程是否已启动。</param>
	/// <param name="batchNo">当前批次号。</param>
	/// <remarks>
	/// 由 BuildShakeProductionEvents 调用，按奇偶号映射到同一采血管的 A/B 顶空瓶。
	/// </remarks>
	private void AddHeadspaceShakeProductionEvent(
		List<TubeProcessEvent> events,
		int headspaceShakeProductionNumber,
		bool isDetectionStarted,
		string batchNo)
	{
		if (_lastHeadspaceShakeProductionNumber == headspaceShakeProductionNumber)
		{
			return;
		}

		_lastHeadspaceShakeProductionNumber = headspaceShakeProductionNumber;
		if (!isDetectionStarted || headspaceShakeProductionNumber <= 0)
		{
			return;
		}

		int tubeIndex = (headspaceShakeProductionNumber + 1) / 2;
		string headspaceBottleTag = headspaceShakeProductionNumber % 2 != 0 ? "A" : "B";
		string message = $"顶空瓶摇晃当前生产号={headspaceShakeProductionNumber}，对应采血管{tubeIndex}{headspaceBottleTag}。";
		events.Add(new TubeProcessEvent
		{
			Timestamp = DateTime.Now,
			BatchNo = batchNo,
			TubeIndex = tubeIndex,
			HeadspaceBottleTag = headspaceBottleTag,
			ProcessName = "顶空瓶摇匀",
			EventName = "摇晃编号",
			PlcValue = headspaceShakeProductionNumber.ToString(CultureInfo.InvariantCulture),
			Note = message,
			HomeLogLevel = HomeLogLevel.Info,
			HomeLogSource = HomeLogSource.Process,
			HomeLogKind = HomeLogKind.Detection,
			HomeLogMessage = message,
			PersistHomeLogToFile = true
		});
	}

	/// <summary>
	/// 用新集合替换槽位状态集合并返回是否发生变化。
	/// </summary>
	/// By:ChengLei
	/// <param name="target">当前状态集合。</param>
	/// <param name="source">新状态集合。</param>
	/// <returns>返回集合内容是否发生变化。</returns>
	/// <remarks>
	/// 由 ApplyRegisters 调用，用于减少无效界面刷新。
	/// </remarks>
	private static bool ReplaceSlotSet(HashSet<int> target, HashSet<int> source)
	{
		if (target.SetEquals(source))
		{
			return false;
		}

		target.Clear();
		foreach (int item in source)
		{
			target.Add(item);
		}

		return true;
	}

	/// <summary>
	/// 更新当前已使用移液枪头数量并返回是否发生变化。
	/// </summary>
	/// By:ChengLei
	/// <param name="usedCount">最新已使用枪头数量。</param>
	/// <returns>返回数量是否发生变化。</returns>
	/// <remarks>
	/// 由 ApplyRegisters 调用，用于控制枪头区颜色刷新。
	/// </remarks>
	private bool UpdateNeedleHeadUsedCount(int usedCount)
	{
		int safeUsedCount = Math.Clamp(usedCount, 0, _maxNeedleHeadCount);
		if (_usedNeedleHeadCount == safeUsedCount)
		{
			return false;
		}

		_usedNeedleHeadCount = safeUsedCount;
		return true;
	}
}
