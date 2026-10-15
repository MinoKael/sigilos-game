extends SceneTree

# Injeta toque de verdade (Input.parse_input_event, que liga a emulação do mouse) numa lista e registra a rolagem.
# --target=NomeDoNó : o controle sob o dedo (primeiro nó com esse nome dentro do primeiro ScrollContainer visível)
var _frame := 0
var _step := 0
var _scroll: ScrollContainer
var _log: PackedStringArray = []
var _trial := 0
var _points: Array = []
var _before := 0
var _phase := 0
var _start := Vector2.ZERO
var _name := ""
var _kinetic := 0
var _target_name := "Rune"

func _initialize():
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--target="):
			_target_name = arg.substr(9)
	root.add_child(load("res://Scenes/GameRoot.tscn").instantiate())

func _find_scroll(node: Node) -> ScrollContainer:
	var best: ScrollContainer = null
	for c in node.find_children("*", "ScrollContainer", true, false):
		var s := c as ScrollContainer
		if s.is_visible_in_tree() and s.get_v_scroll_bar().max_value > s.size.y + 50:
			if best == null or s.size.y * s.size.x > best.size.y * best.size.x:
				best = s
	return best

func _first_child_named(scroll: ScrollContainer, prefix: String) -> Control:
	var matches := scroll.find_children("*", "BaseButton", true, false) if prefix == "@button" else scroll.find_children(prefix + "*", "Control", true, false)
	for m in matches:
		var c := m as Control
		if c.is_visible_in_tree() and scroll.get_global_rect().encloses(c.get_global_rect()):
			return c
	return null

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

func _process(_d):
	_frame += 1
	if _frame < 100:
		return false
	if _scroll == null:
		_scroll = _find_scroll(root)
		if _scroll == null:
			print("TOUCH no scroll")
			return true
		var tile := _first_child_named(_scroll, _target_name)
		var rect := _scroll.get_global_rect()
		if tile != null:
			var r := tile.get_global_rect()
			_points = [["on_" + tile.name.substr(0, 8), r.get_center()], ["gap", Vector2(r.end.x + 4, r.get_center().y)]]
		else:
			_points = [["center", rect.get_center()]]
		print("TOUCH scroll=", _scroll.name, " rect=", rect, " max=", _scroll.get_v_scroll_bar().max_value, " points=", _points)
		return false
	# máquina: por ponto: reset, press, 10 drags, release, espera 40 quadros, mede
	var t := _trial
	if t >= _points.size():
		print("TOUCH live_tiles=", _scroll.find_children("Rune*", "Control", true, false).size(), " scroll=", _scroll.scroll_vertical)
		print("TOUCH done")
		return true
	var p = _points[t]
	_phase += 1
	if _phase == 1:
		_scroll.scroll_vertical = 0
	elif _phase == 20:
		_before = _scroll.scroll_vertical
		_start = p[1]
		_touch(_start, true)
	elif _phase > 20 and _phase <= 32:
		var i := _phase - 20
		_drag(_start + Vector2(0, -20 * i), Vector2(0, -20))
	elif _phase == 33:
		_touch(_start + Vector2(0, -240), false)
	elif _phase == 34:
		print("TOUCH ", p[0], " scroll_after_release=", _scroll.scroll_vertical - _before)
	elif _phase == 100:
		print("TOUCH ", p[0], " after_inertia=", _scroll.scroll_vertical - _before)
		_trial += 1
		_phase = 0
	return false
