extends SceneTree

# Mede uma tela: nós, chamadas de desenho e tempo de quadro. --label=nome --seconds=N (padrão 3)
var _label := ""
var _seconds := 3.0
var _tap := ""
var _frame := 0
var _t0 := 0
var _last := 0
var _times: Array = []
var _draw := 0.0
var _n := 0
var _spikes := 0

func _initialize():
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--label="):
			_label = arg.substr(8)
		elif arg.begins_with("--tap="):
			_tap = arg.substr(6)
		elif arg.begins_with("--seconds="):
			_seconds = float(arg.substr(10))
	root.add_child(load("res://Scenes/GameRoot.tscn").instantiate())
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(), true)

func _count(node: Node, stats: Dictionary):
	stats.nodes += 1
	if node is CanvasItem:
		if node.material is ShaderMaterial:
			stats.shaders += 1
	if node.get_script() != null and node.is_processing():
		stats.processing += 1
	for c in node.get_children():
		_count(c, stats)

func _process(_delta):
	_frame += 1
	if _frame == 60 and _tap != "":
		var node = root.find_child(_tap, true, false)
		if node != null:
			node.button_pressed = true
	if _frame < 120:
		return false
	var now := Time.get_ticks_usec()
	if _frame == 120:
		_t0 = now
		_last = now
		return false
	if (now - _last) / 1000.0 > 8.0 and _spikes < 12 and (now - _t0) / 1000000.0 > 3.0:
		_spikes += 1
		print("SPIKE t=%.2fs frame_ms=%.1f draw=%d objs=%d proc_ms=%.1f render_cpu=%.1f" % [(now - _t0) / 1000000.0, (now - _last) / 1000.0, RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME), Performance.get_monitor(Performance.OBJECT_COUNT), Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0, RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid())])
	_times.append((now - _last) / 1000.0)
	_last = now
	_draw += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME)
	_n += 1
	if (now - _t0) / 1000000.0 >= _seconds:
		var sorted := _times.duplicate()
		sorted.sort()
		var sum := 0.0
		for t in sorted:
			sum += t
		var stats := {"nodes": 0, "shaders": 0, "processing": 0}
		_count(root, stats)
		print("PERF %s | frames=%d avg=%.2f p50=%.2f p99=%.2f max=%.2f | draw=%.0f gpu=%.2f | nodes=%d shaders=%d proc=%d objs=%d" % [
			_label, _n, sum / _n, sorted[_n / 2], sorted[int(_n * 0.99)], sorted[_n - 1],
			_draw / _n, RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()),
			stats.nodes, stats.shaders, stats.processing, Performance.get_monitor(Performance.OBJECT_COUNT)])
		return true
	return false
