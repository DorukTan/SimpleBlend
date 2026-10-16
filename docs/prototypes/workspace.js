'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const canvas = $('surface');
  const context = canvas.getContext('2d');
  const clone = value => JSON.parse(JSON.stringify(value));
  const layers = [
    { name: 'Stone', hint: 'Unpainted base', channel: 'Base', color: '#8e8b7b', swatch: 'stone' },
    { name: 'Moss', hint: 'First overlay', channel: 'Red', color: '#687b3f', swatch: 'moss' },
    { name: 'Grime', hint: 'Second overlay', channel: 'Green', color: '#70604a', swatch: 'grime' }
  ];
  let state, history = [], redo = [], gesture = null, pendingReset = null;
  function initial(target = 'empty') {
    return { target, scenario: target === 'empty' ? 'empty' : target === 'cube' ? 'sparse' : 'setup', task: 'paint', prepared: false, dense: target !== 'cube', material: false, active: false, layer: 1, dirty: false, preview: false, strokes: [] };
  }
  function setStatus(text) { $('status').textContent = text; }
  function reset(target, scenario) {
    state = initial(target);
    state.scenario = scenario || state.scenario;
    if (['ready', 'stale', 'error'].includes(state.scenario)) { state.prepared = true; state.material = true; }
    history = []; redo = []; gesture = null;
    $('target').value = target;
    $('scenario').value = state.scenario;
    $('workflow').value = 'layers';
    $('recovery-details').hidden = true;
    $('plan-details').hidden = true;
    setStatus('Choose a surface, then start painting. Design preview only.');
    render();
  }
  function requestReset(target, scenario) {
    if ($('lock').checked && state.active) {
      $('target').value = state.target; $('scenario').value = state.scenario;
      setStatus('Target is locked. Deactivate painting or turn off selection lock first.');
      return;
    }
    if (state.dirty || state.preview) {
      pendingReset = { target, scenario }; $('reset-dialog').showModal(); return;
    }
    reset(target, scenario);
  }
  function changeTask(task) {
    if (gesture) finishGesture();
    state.preview = false; state.active = false; state.task = task;
    $('plan-details').hidden = true; render();
    setStatus(task === 'paint' ? 'Choose a layer, then activate painting.' : 'Review workflow only; this does not implement the Unity operation.');
    if (task === 'materials') $('make-material').focus();
    else if (task === 'paint' && state.prepared) $('layer-list').children[state.layer]?.focus();
  }
  function commit(strokes) {
    history.push(clone(state.strokes)); redo = [];
    state.strokes = clone(strokes); state.dirty = true; render();
  }
  function undo() {
    if (!history.length || state.preview || gesture) return;
    redo.push(clone(state.strokes)); state.strokes = history.pop(); state.dirty = true; render();
    setStatus('Undid one complete demo operation.');
  }
  function redoOperation() {
    if (!redo.length || state.preview || gesture) return;
    history.push(clone(state.strokes)); state.strokes = redo.pop(); state.dirty = true; render();
    setStatus('Redid one complete demo operation.');
  }
  function treatmentStroke() {
    return { points: [{ x: .17, y: .72 }, { x: .82, y: .72 }], layer: 2, erase: false, radius: .6, strength: .55 };
  }
  function draw() {
    const w = canvas.width, h = canvas.height;
    context.clearRect(0, 0, w, h); context.fillStyle = '#383c3e'; context.fillRect(0, 0, w, h);
    if (state.target === 'empty') return;
    const x = w * .16, y = h * .2, width = w * .68, height = h * .57;
    context.fillStyle = '#77786f'; context.fillRect(x, y, width, height);
    context.save(); context.beginPath(); context.rect(x, y, width, height); context.clip();
    for (const stroke of [...state.strokes, ...(state.preview ? [treatmentStroke()] : []), ...(gesture ? [gesture.stroke] : [])]) {
      context.strokeStyle = stroke.erase ? '#77786f' : stroke.color || layers[stroke.layer].color;
      context.globalAlpha = stroke.strength; context.lineWidth = stroke.radius * w / 8;
      context.lineCap = 'round'; context.lineJoin = 'round'; context.beginPath();
      stroke.points.forEach((p, index) => index ? context.lineTo(p.x * w, p.y * h) : context.moveTo(p.x * w, p.y * h));
      if (stroke.points.length === 1) context.lineTo(stroke.points[0].x * w + .1, stroke.points[0].y * h);
      context.stroke();
    }
    context.globalAlpha = 1;
    if ($('preview').value === 'density') {
      const divisions = state.target === 'cube' && !state.dense ? 1 : 16;
      context.fillStyle = '#d8de94';
      for (let a = 0; a <= divisions; a++) for (let b = 0; b <= divisions; b++) {
        context.beginPath(); context.arc(x + a * width / divisions, y + b * height / divisions, 2.3, 0, Math.PI * 2); context.fill();
      }
    }
    context.restore();
    context.strokeStyle = '#adad8f'; context.lineWidth = 2; context.strokeRect(x, y, width, height);
  }
  function render() {
    $('scenario').value = state.scenario;
    const blocked = ['empty', 'mixed', 'unsupported', 'stale', 'error'].includes(state.scenario);
    const named = state.target === 'cube' ? 'Cube' : state.target === 'long' ? 'Environment / Modular_Wall_Exterior_Weathered_Section_01' : 'Lesson Wall';
    const counts = state.prepared ? state.target === 'cube' && !state.dense ? 'Existing corner vertices · coarse painting' : 'Ready to paint · original mesh unchanged' : state.target === 'cube' ? 'Low-detail mesh · small strokes need more vertices' : 'Set up this object to begin';
    $('target-meta').textContent = state.target === 'empty' ? 'Mesh and material readiness will appear here.' : counts;
    $('save-state').textContent = state.target === 'empty' ? 'No target' : state.dirty ? 'Unsaved demo changes' : 'Demo state clean';
    $('scene-title').textContent = state.target === 'empty' ? 'Select a mesh to begin' : named;
    $('scene-caption').textContent = state.preview ? 'Preview only · Apply or Cancel' : state.active ? `Painting ${$('workflow').value === 'rgba' ? $('channel').value : layers[state.layer].name} · drag across the surface` : 'Choose a surface and start painting. Browser sketch, not a Unity render.';
    $('tool-status').textContent = state.active ? `Paint · ${$('workflow').value === 'rgba' ? $('channel').value : layers[state.layer].name}` : 'Tool inactive';
    $('gesture-hint').textContent = state.active ? 'Drag to paint · Shift to erase · Escape to stop · Ctrl/Cmd+Z to undo' : 'Your scene is unchanged until you choose an action.';
    $('brush-surface').textContent = $('workflow').value === 'rgba' ? $('channel').value : layers[state.layer].name;
    for (const button of document.querySelectorAll('[data-task]')) button.setAttribute('aria-pressed', String(button.dataset.task === state.task));
    const notices = {
      empty: ['Choose a target', 'Select Lesson Wall, or switch to Create to review the new-mesh workflow.', ''],
      setup: ['A quick setup first', 'Set up this object, then choose the surfaces you want to paint.', ''],
      sparse: ['This mesh is too sparse for small face strokes', 'Vertex painting needs vertices beneath the brush. Prepare a subdivided copy or deliberately keep coarse painting.', 'warning'],
      ready: ['Ready to paint', 'Original mesh unchanged. Choose a surface and start painting.', ''],
      mixed: ['Mixed selection · 2 supported, 1 unsupported', 'Nothing will be changed partially. Narrow the selection; batch preflight is planned in #56.', 'warning'],
      unsupported: ['Unsupported target · camera selected', 'Painting requires a supported mesh renderer. Select a mesh or create one.', 'warning'],
      stale: ['Source topology changed', 'Saved paint is retained but cannot safely use the new vertex indices.', 'error'],
      error: ['Could not save the output mesh', 'Example: destination is read-only. Your valid paint and settings are retained.', 'error']
    };
    const notice = notices[state.scenario];
    if (state.scenario === 'ready' && !state.material && $('workflow').value === 'layers') {
      notice[0] = 'Choose your surfaces';
      notice[1] = 'Use Change surfaces to add a material. Custom shader users can choose color channels under More paint options.';
    }
    $('notice').replaceChildren();
    const strong = document.createElement('strong'); strong.textContent = notice[0];
    const message = document.createElement('p'); message.textContent = notice[1];
    $('notice').append(strong, message); $('notice').className = 'notice ' + notice[2];
    $('notice').classList.toggle('ready', state.scenario === 'ready' && state.material);
    $('setup-panel').hidden = state.task !== 'paint' || state.prepared || ['mixed', 'stale', 'error'].includes(state.scenario);
    $('prepare').hidden = ['empty', 'unsupported'].includes(state.scenario);
    $('prepare').textContent = state.target === 'cube' ? 'Make a more detailed paint copy' : 'Set up painting';
    $('keep-coarse').hidden = state.target !== 'cube' || state.prepared;
    $('select-wall').hidden = !['empty', 'unsupported'].includes(state.scenario);
    $('paint-panel').hidden = state.task !== 'paint' || !state.prepared || blocked || state.preview;
    $('materials-panel').hidden = state.task !== 'materials';
    $('make-material').disabled = blocked || !state.prepared;
    $('make-material').title = !state.prepared ? 'Prepare a supported target first.' : '';
    $('future-panel').hidden = !['sculpt', 'create', 'output'].includes(state.task);
    const descriptions = {
      sculpt: ['Shape an owned mesh', 'Preflight ownership, choose a sculpt or topology tool, then commit one undoable gesture. Sculpting is planned in M14/M15.'],
      create: ['Create paint-ready geometry', 'Choose a wall or floor, set dimensions and density, inspect a bounded preview, then generate an owned result. Generators are planned in M16.'],
      output: ['Finalize and share', 'Review a new mesh asset, material dependencies and optional authoring-data removal. Baking is planned in #53.']
    };
    if (descriptions[state.task]) { $('future-title').textContent = descriptions[state.task][0]; $('future-copy').textContent = descriptions[state.task][1]; }
    $('plan-action').textContent = state.task === 'output' ? 'Review baked output' : 'Review operation steps';
    $('plan-action').disabled = state.task !== 'create' && (blocked || !state.prepared);
    $('plan-action').title = $('plan-action').disabled ? 'A supported, prepared target is required.' : '';
    $('preview-panel').hidden = !state.preview;
    $('recovery-panel').hidden = !['stale', 'error'].includes(state.scenario);
    $('recovery-title').textContent = state.scenario === 'stale' ? 'Recover without losing paint' : 'Choose a writable destination';
    $('recovery-copy').textContent = state.scenario === 'stale' ? 'Review transfer or restore matching source topology. Reapply is not a remapping operation.' : 'Retry the simulated operation; your inputs and paint stay intact.';
    $('recover').textContent = state.scenario === 'stale' ? 'Review transfer plan' : 'Retry demo operation';
    $('layer-list').hidden = $('workflow').value === 'rgba'; $('rgba-fields').hidden = $('workflow').value !== 'rgba';
    $('layer-list').replaceChildren();
    layers.forEach((layer, index) => {
      const button = document.createElement('button'); button.className = 'layer'; button.setAttribute('aria-pressed', String(index === state.layer));
      button.setAttribute('aria-label', 'Paint with ' + layer.name);
      const swatch = document.createElement('span'); swatch.className = 'swatch ' + layer.swatch; swatch.setAttribute('aria-hidden', 'true');
      const label = document.createElement('span'); label.className = 'layer-info'; label.textContent = layer.name;
      const hint = document.createElement('small'); hint.textContent = layer.hint; label.append(hint);
      const channel = document.createElement('span'); channel.className = 'channel'; channel.textContent = layer.channel;
      button.append(swatch, label, channel);
      button.onclick = () => { state.layer = index; render(); $('layer-list').children[index].focus(); };
      $('layer-list').append(button);
    });
    $('activate').disabled = blocked || !state.prepared || (!state.material && $('workflow').value === 'layers');
    $('activate').title = !state.material && $('workflow').value === 'layers' ? 'Assign a compatible material or choose Direct RGBA first.' : '';
    $('activate').textContent = state.active ? 'Stop painting' : 'Start painting';
    $('undo').disabled = !history.length || state.preview || !!gesture;
    $('redo').disabled = !redo.length || state.preview || !!gesture;
    $('sample-stroke').disabled = !state.active || state.preview;
    $('save').disabled = !state.dirty || state.preview || !!gesture;
    draw();
  }
  function position(event) { const rect = canvas.getBoundingClientRect(); return { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height }; }
  function illustrativeColor() {
    if ($('workflow').value !== 'rgba') return layers[state.layer].color;
    return { Red: '#d66060', Green: '#67ad70', Blue: '#6d94db', Alpha: '#e5e5e5', RGB: '#bc86c9' }[$('channel').value];
  }
  function finishGesture() {
    if (!gesture) return;
    const stroke = gesture.stroke; gesture = null;
    commit([...state.strokes, stroke]); setStatus('One demo stroke applied. Undo reverses the complete gesture.');
  }
  canvas.addEventListener('pointerdown', event => {
    const p = position(event);
    if (!state.active || state.preview || event.button !== 0 || event.altKey || p.x < .16 || p.x > .84 || p.y < .2 || p.y > .77) return;
    canvas.setPointerCapture(event.pointerId);
    gesture = { stroke: { points: [p], layer: state.layer, color: illustrativeColor(), erase: event.shiftKey || $('operation').value === 'erase', radius: +$('radius').value, strength: +$('strength').value } };
    canvas.focus(); draw();
  });
  canvas.addEventListener('pointermove', event => { if (gesture) { gesture.stroke.points.push(position(event)); draw(); } });
  canvas.addEventListener('pointerup', finishGesture);
  canvas.addEventListener('pointercancel', () => { gesture = null; render(); setStatus('Demo gesture canceled.'); });
  $('theme').onchange = event => { document.documentElement.dataset.theme = event.target.value; };
  $('dock').onchange = event => { document.documentElement.style.setProperty('--dock', event.target.value + 'px'); };
  $('target').onchange = event => requestReset(event.target.value);
  $('scenario').onchange = event => {
    const scenario = event.target.value;
    requestReset(scenario === 'empty' ? 'empty' : scenario === 'sparse' ? 'cube' : 'wall', scenario);
  };
  $('select-wall').onclick = () => { requestReset('wall'); if (state.target === 'wall') $('prepare').focus(); };
  $('prepare').onclick = () => { state.prepared = true; state.dense = true; state.scenario = 'ready'; render(); $('open-materials').focus(); setStatus('Prepared demo paint data. Material setup or Direct RGBA is next.'); };
  $('keep-coarse').onclick = () => { state.prepared = true; state.dense = false; render(); $('open-materials').focus(); setStatus('Existing vertices retained. Small face-center detail still requires a denser mesh.'); };
  $('open-materials').onclick = () => changeTask('materials');
  $('make-material').onclick = () => { state.material = true; changeTask('paint'); $('layer-list').children[state.layer].focus(); setStatus('Lesson material assigned in this demonstration only. Activate painting when ready.'); };
  $('workflow').onchange = () => { state.active = false; render(); };
  $('channel').onchange = render;
  $('activate').onclick = () => { state.active = !state.active; render(); setStatus(state.active ? 'Paint demonstration active. Drag on the wall or use Sample stroke.' : 'Painting stopped; Scene navigation owns input.'); };
  for (const name of ['radius', 'strength']) $(name).oninput = () => { $(name + '-value').textContent = (+$(name).value).toFixed(2) + (name === 'radius' ? ' m' : ''); };
  $('preview').onchange = draw;
  $('undo').onclick = undo; $('redo').onclick = redoOperation;
  $('sample-stroke').onclick = () => commit([...state.strokes, { points: [{ x: .32, y: .57 }, { x: .67, y: .44 }], layer: state.layer, color: illustrativeColor(), erase: $('operation').value === 'erase', radius: +$('radius').value, strength: +$('strength').value }]);
  $('save').onclick = () => { state.dirty = false; render(); setStatus('Demo marked clean for this session only. In Unity, save the scene or prefab and verify after reopening.'); };
  $('preview-grime').onclick = () => { state.preview = true; state.active = false; render(); $('apply-preview').focus(); setStatus('Preview only. Apply or cancel before another operation.'); };
  $('cancel-preview').onclick = () => { state.preview = false; render(); $('preview-grime').focus(); setStatus('Preview canceled. Saved demo strokes are unchanged.'); };
  $('apply-preview').onclick = () => { state.preview = false; commit([...state.strokes, treatmentStroke()]); $('preview-grime').focus(); setStatus('Applied one illustrative treatment. This is not the planned procedural algorithm.'); };
  $('recover').onclick = () => { if (state.scenario === 'error') { state.scenario = 'ready'; render(); setStatus('Simulated retry succeeded; no real output was written.'); } else $('recovery-details').hidden = false; };
  $('cancel-recovery').onclick = () => { $('recovery-details').hidden = true; setStatus('Recovery canceled; incompatible paint remains retained and blocked.'); };
  $('plan-action').onclick = () => {
    if (state.task === 'output') { $('output-dialog').showModal(); return; }
    $('plan-details').replaceChildren();
    const p = document.createElement('p'); p.textContent = state.task === 'create' ? 'Dimensions → density and count estimate → material/UVs → bounded preview → explicit Generate. Cancel leaves no assets. The generator is planned, not simulated as working.' : 'Validate owned geometry → choose tool → preview or gesture → one Undo → save. Topology-owner conflicts must be resolved first.';
    $('plan-details').append(p); $('plan-details').hidden = false;
  };
  $('cancel-output').onclick = () => $('output-dialog').close();
  $('confirm-output').onclick = () => { $('output-dialog').close(); setStatus('Bake flow reviewed. No mesh was exported or component removed.'); };
  $('keep-demo').onclick = () => { $('reset-dialog').close(); $('target').value = state.target; $('scenario').value = state.scenario; pendingReset = null; };
  $('reset-dialog').addEventListener('cancel', () => { $('target').value = state.target; $('scenario').value = state.scenario; pendingReset = null; });
  $('reset-demo').onclick = () => { $('reset-dialog').close(); const pending = pendingReset; pendingReset = null; reset(pending.target, pending.scenario); };
  $('frame').onclick = () => { draw(); setStatus('Demonstration surface framed.'); };
  document.querySelectorAll('[data-task]').forEach(button => button.onclick = () => changeTask(button.dataset.task));
  document.addEventListener('keydown', event => {
    if (document.querySelector('dialog[open]')) return;
    if (['INPUT', 'SELECT', 'TEXTAREA'].includes(event.target.tagName)) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') { event.preventDefault(); event.shiftKey ? redoOperation() : undo(); }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') { event.preventDefault(); redoOperation(); }
    if (event.key === 'Escape') {
      const wasPreview = state.preview;
      gesture = null; state.preview = false; state.active = false; render();
      if (wasPreview) $('preview-grime').focus();
      else if (!$('activate').disabled && !$('paint-panel').hidden) $('activate').focus();
      setStatus('Preview or gesture canceled; painting deactivated.');
    }
  });
  reset('empty');
})();
