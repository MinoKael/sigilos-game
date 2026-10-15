extends SceneTree

# Gestos de toque de verdade (Input.parse_input_event) e o que eles fazem.
#   --mode=tap    : toque curto sobre o primeiro botão da lista; deve disparar 1 vez
#   --mode=drag   : arrasto que começa sobre esse botão; deve disparar 0 vezes e rolar a lista
#   --mode=mouse  : o mesmo arrasto de drag, mas com o mouse do PC (sem override.cfg): clicar, arrastar e soltar
#   --mode=axis   : numa lista que rola de lado (Prep): arrasto horizontal e vertical sobre ela; mostra quem rolou
#   --mode=fling  : 8 arremessos seguidos na lista maior; tempo de quadro e objetos depois de cada um (e os quadros acima de 8 ms nos dois primeiros)
#   --mode=slider : abre os Ajustes e arrasta um controle deslizante; mostra se ele mexeu
var _mode := "tap"
var _frame := 0
var _phase := 0
var _trial := 0
var _scroll: ScrollContainer
var _button: BaseButton
var _count := 0
var _start := Vector2.ZERO
var _before := Vector2.ZERO
var _times: Array = []
var _last := 0
var _outer: ScrollContainer
var _log: Array = []
var _slider: Slider
var _slider_before := 0.0

func _initialize():
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--mode="):
			_mode = arg.substr(7)
	root.add_child(load("res://Scenes/GameRoot.tscn").instantiate())

func _scrolls() -> Array:
	var found: Array = []
	for c in root.find_children("*", "ScrollContainer", true, false):
		var s := c as ScrollContainer
		if s.is_visible_in_tree():
			found.append(s)
	return found

func _biggest(vertical: bool) -> ScrollContainer:
	var best: ScrollContainer = null
	for s in _scrolls():
		var bar: ScrollBar = s.get_v_scroll_bar() if vertical else s.get_h_scroll_bar()
		if bar.max_value > bar.page + 50 and (best == null or s.size.x * s.size.y > best.size.x * best.size.y):
			best = s
	return best

func _touch(pos: Vector2, pressed: bool):
	var e := InputEventScreenTouch.new()
	e.index = 0
	e.position = pos
	e.pressed = pressed
	Input.parse_input_event(e)

func _drag(pos: Vector2, rel: Vector2):
	var e := InputEventScreenDrag.new()
	e.index = 0
	e.position = pos
	e.relative = rel
	e.velocity = rel * 60
	Input.parse_input_event(e)

func _mouse(pos: Vector2, pressed: bool):
	var e := InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.position = pos
	e.global_position = pos
	e.pressed = pressed
	e.button_mask = MOUSE_BUTTON_MASK_LEFT if pressed else 0
	Input.parse_input_event(e)

func _motion(pos: Vector2, rel: Vector2):
	var e := InputEventMouseMotion.new()
	e.position = pos
	e.global_position = pos
	e.relative = rel
	e.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(e)

func _first_button(scroll: ScrollContainer) -> BaseButton:
	for b in scroll.find_children("*", "BaseButton", true, false):
		var c := b as BaseButton
		if c.is_visible_in_tree() and scroll.get_global_rect().encloses(c.get_global_rect()):
			return c
	return null

func _process(_d):
	_frame += 1
	if _frame < 100:
		return false
	match _mode:
		"tap", "drag", "mouse": return _run_tap()
		"axis": return _run_axis()
		"fling": return _run_fling()
		"slider": return _run_slider()
	return true

func _run_tap() -> bool:
	if _scroll == null:
		_scroll = _biggest(true)
		if _scroll == null:
			print("GESTURE tap: sem lista")
			return true
		_button = _first_button(_scroll)
		if _button == null:
			print("GESTURE tap: sem botão")
			return true
		_button.pressed.connect(func():
			_count += 1
			print("GESTURE ", _mode, " disparou (", _count, ")"))
		_start = _button.get_global_rect().get_center()
		print("GESTURE tap botão=", _button.name, " em ", _start)
		return false
	_phase += 1
	if _mode == "tap":
		if _phase == 5:
			_touch(_start, true)
		elif _phase == 8:
			_touch(_start, false)
		elif _phase == 40:
			print("GESTURE tap disparos=", _count)
			return true
		return false
	if _phase == 5:
		if _mode == "mouse":
			_mouse(_start, true)
		else:
			_touch(_start, true)
	elif _phase > 5 and _phase <= 17:
		if _mode == "mouse":
			_motion(_start + Vector2(0, -20 * (_phase - 5)), Vector2(0, -20))
		else:
			_drag(_start + Vector2(0, -20 * (_phase - 5)), Vector2(0, -20))
	elif _phase == 18:
		if _mode == "mouse":
			_mouse(_start + Vector2(0, -240), false)
		else:
			_touch(_start + Vector2(0, -240), false)
	elif _phase == 60:
		print("GESTURE ", _mode, " disparos=", _count, " rolou=", _scroll.scroll_vertical)
		return true
	return false

func _run_axis() -> bool:
	if _scroll == null:
		_scroll = _biggest(false)
		if _scroll == null:
			print("GESTURE axis: sem lista horizontal")
			return true
		_outer = null
		var n: Node = _scroll.get_parent()
		while n != null:
			if n is ScrollContainer and (n as ScrollContainer).get_v_scroll_bar().max_value > (n as ScrollContainer).get_v_scroll_bar().page + 1:
				_outer = n
				break
			n = n.get_parent()
		_start = _scroll.get_global_rect().get_center()
		print("GESTURE axis lista=", _scroll.name, " rect=", _scroll.get_global_rect(), " h_max=", _scroll.get_h_scroll_bar().max_value, " pagina_externa=", _outer.name if _outer else "nenhuma")
		return false
	_phase += 1
	var rel := Vector2(-20, 0) if _trial == 0 else Vector2(0, -20)
	if _phase == 5:
		_before = Vector2(_scroll.scroll_horizontal, _outer.scroll_vertical if _outer else 0)
		_touch(_start, true)
	elif _phase > 5 and _phase <= 17:
		_drag(_start + rel * (_phase - 5), rel)
	elif _phase == 18:
		_touch(_start + rel * 12, false)
	elif _phase == 60:
		var after := Vector2(_scroll.scroll_horizontal, _outer.scroll_vertical if _outer else 0)
		print("GESTURE axis ", "horizontal" if _trial == 0 else "vertical", " fileira_rolou=", after.x - _before.x, " pagina_rolou=", after.y - _before.y)
		_trial += 1
		_phase = 0
		if _trial >= 2:
			return true
	return false

func _run_fling() -> bool:
	if _scroll == null:
		_scroll = _biggest(true)
		if _scroll == null:
			print("GESTURE fling: sem lista")
			return true
		_start = _scroll.get_global_rect().get_center()
		_last = Time.get_ticks_usec()
		return false
	var now := Time.get_ticks_usec()
	_times.append((now - _last) / 1000.0)
	var rid := root.get_viewport_rid()
	RenderingServer.viewport_set_measure_render_time(rid, true)
	_log.append([(now - _last) / 1000.0, RenderingServer.viewport_get_measured_render_time_cpu(rid) + RenderingServer.get_frame_setup_time_cpu(), RenderingServer.viewport_get_measured_render_time_gpu(rid), _phase])
	_last = now
	_phase += 1
	var dir := -1 if _trial % 2 == 0 else 1
	if _phase == 3:
		_touch(_start, true)
	elif _phase > 3 and _phase <= 9:
		_drag(_start + Vector2(0, 90 * dir * (_phase - 3)), Vector2(0, 90 * dir))
	elif _phase == 10:
		_touch(_start + Vector2(0, 540 * dir), false)
	elif _phase == 130:
		var sorted := _times.duplicate()
		sorted.sort()
		var n := sorted.size()
		print("GESTURE fling #%d frames=%d p50=%.2f p99=%.2f max=%.2f objs=%d nodes=%d scroll=%d" % [_trial + 1, n, sorted[n / 2], sorted[int(n * 0.99)], sorted[n - 1], Performance.get_monitor(Performance.OBJECT_COUNT), Performance.get_monitor(Performance.OBJECT_NODE_COUNT), _scroll.scroll_vertical])
		if _trial <= 1:
			for row in _log:
				if row[0] > 8.0:
					print("GESTURE lento ms=%.1f render_cpu=%.1f gpu=%.1f phase=%d" % row)
		_log.clear()
		_times.clear()
		_trial += 1
		_phase = 0
		if _trial >= 8:
			return true
	return false

func _run_slider() -> bool:
	_phase += 1
	if _phase == 1:
		var buttons := root.find_children("Settings*", "BaseButton", true, false)
		for n in ["Config", "Settings", "Ajustes"]:
			var c := root.find_child(n, true, false)
			if c != null and c is BaseButton:
				(c as BaseButton).emit_signal("pressed")
				print("GESTURE slider abriu ", n)
				return false
		print("GESTURE slider: sem botão de ajustes")
		return true
	if _phase == 40:
		var sliders := root.find_children("*", "Slider", true, false)
		for s in sliders:
			if (s as Slider).is_visible_in_tree():
				_slider = s
				break
		if _slider == null:
			print("GESTURE slider: sem controle")
			return true
		_slider_before = _slider.value
		_start = _slider.get_global_rect().get_center()
		print("GESTURE slider ", _slider.name, " valor=", _slider_before, " em ", _start, " rect=", _slider.get_global_rect())
		return false
	if _slider != null:
		if _phase == 45:
			_touch(_start, true)
		elif _phase > 45 and _phase <= 55:
			_drag(_start - Vector2(15 * (_phase - 45), 0), Vector2(-15, 0))
		elif _phase == 56:
			_touch(_start - Vector2(150, 0), false)
		elif _phase == 80:
			print("GESTURE slider valor_depois=", _slider.value)
			return true
	return false
