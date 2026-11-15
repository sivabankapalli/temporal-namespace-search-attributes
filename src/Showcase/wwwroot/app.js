'use strict';
const $ = id => document.getElementById(id);
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let catalog, config, selected, activeExample = null, lastScoped = '', searching = false, currentDetail = null;
let searchVersion = 0, searchController = null;
function notice(message, error = false) { $('notice').textContent = message; $('notice').classList.toggle('error', error); }
async function api(path, body, requestOptions = {}) {
  const options = {...requestOptions};
  if (body !== undefined) Object.assign(options, {method:'POST',headers:{'Content-Type':'application/json','X-Showcase-Action':'1'},body:JSON.stringify(body)});
  const response = await fetch(path, options);
  let result;
  try { result = await response.json(); } catch { throw new Error(`HTTP ${response.status}. Check the terminal running the demo.`); }
  if (!response.ok) throw new Error((result.error || `Request failed (${response.status})`) + (result.grpcCode ? ` [${result.grpcCode}]` : '') + (result.hint ? ` ${result.hint}` : ''));
  return result;
}
function showInfo(title, html) { $('infoTitle').textContent = title; $('infoContent').innerHTML = html; if (!$('infoDialog').open) $('infoDialog').showModal(); }
function resetSearch(message) {
  searchVersion++; searchController?.abort(); searching = false; $('searchButton').disabled = false;
  lastScoped = ''; $('copyButton').disabled = true; $('scopedQuery').textContent = 'No completed query for this selection.';
  $('matchCount').textContent = '-'; $('fetched').textContent = message;
  $('baselineStatus').textContent = 'No comparison yet'; $('baselineStatus').className = 'baseline-status';
  $('rows').innerHTML = `<tr><td colspan="4" class="empty">${escapeHtml(message)}</td></tr>`;
  $('countNote').textContent = 'Returned rows will come from the connected Temporal namespace. Illustrative values above are not result data.';
}
function updateSelection() {
  document.querySelectorAll('[data-example]').forEach(b => {const on = b.dataset.example === activeExample?.id; b.classList.toggle('active', on); b.setAttribute('aria-pressed',String(on));});
  document.querySelectorAll('[data-type]').forEach(b => {const on = b.dataset.type === activeExample?.type; b.classList.toggle('active', on); b.setAttribute('aria-pressed',String(on));});
  document.querySelectorAll('[data-scenario]').forEach(b => b.classList.toggle('active', b.dataset.scenario === selected?.id));
}
function selectExample(id) {
  const example = catalog.typeExamples.find(e => e.id === id); if (!example) return;
  resetSearch('Run this filter to read actual matching executions.');
  activeExample = example; selected = null; updateSelection();
  $('audience').textContent = `${example.type.toUpperCase()} / ${example.attribute}`;
  $('question').textContent = example.title; $('lesson').textContent = example.purpose;
  $('examplePanel').hidden = false; $('attributeLabel').textContent = `${example.attribute} : ${example.type}`;
  $('exampleValue').textContent = example.exampleValue; $('sdkDeclaration').textContent = example.declaration;
  $('sdkAssignment').textContent = example.assignment; $('sourceReference').textContent = 'Source: src/Showcase/' + example.source;
  $('caution').textContent = example.caution; $('query').value = example.query;
  $('expected').textContent = `Fresh baseline: ${example.expectedRefs.length} rows (before changes)`;
  $('valueHeading').textContent = example.attribute + ' / actual indexed value';
  if (config.configured) search();
}
function selectScenario(id) {
  const scenario = catalog.scenarios.find(s => s.id === id); if (!scenario) return;
  resetSearch('Run the selected filter to read actual executions.');
  selected = scenario; activeExample = null; updateSelection(); $('examplePanel').hidden = true;
  $('audience').textContent = 'ADDITIONAL FILTER'; $('question').textContent = scenario.title; $('lesson').textContent = scenario.lesson;
  $('query').value = scenario.query; $('expected').textContent = scenario.baseline ? `Fresh baseline: ${scenario.expectedRefs.length} rows (before changes)` : 'Mutation example: unset the description on PAY-1006';
  $('valueHeading').textContent = 'CorrelationID / actual indexed value';
  if (config.configured) search();
}
function valueHtml(row, attribute) {
  const attributes = row.attributes || {};
  if (!Object.prototype.hasOwnProperty.call(attributes, attribute)) return '<span class="missing">Not set on this execution</span>';
  const value = attributes[attribute];
  if (Array.isArray(value)) return value.length ? value.map(v => `<span class="tag">${escapeHtml(v)}</span>`).join('') : '<code>[]</code>';
  return `<code>${escapeHtml(typeof value === 'string' ? value : JSON.stringify(value))}</code>`;
}
async function search() {
  const query = $('query').value, example = activeExample, scenario = selected;
  const version = ++searchVersion; searchController?.abort(); searchController = new AbortController();
  searching = true; $('searchButton').disabled = true; lastScoped = ''; $('copyButton').disabled = true;
  $('matchCount').textContent = '...'; $('fetched').textContent = 'Reading Temporal Visibility';
  $('baselineStatus').textContent = 'Waiting for actual rows'; $('baselineStatus').className = 'baseline-status';
  $('rows').innerHTML = '<tr><td colspan="4" class="empty">Requesting matching workflows from Temporal...</td></tr>';
  $('countNote').textContent = 'Waiting for the live List and Count responses. No expected rows are inserted into the results.';
  try {
    const result = await api('/api/search?query=' + encodeURIComponent(query), undefined, {signal:searchController.signal});
    if (version !== searchVersion) return;
    lastScoped = result.query; $('scopedQuery').textContent = result.query; $('copyButton').disabled = false;
    $('matchCount').textContent = result.rows.length; $('fetched').textContent = 'Fetched ' + new Date(result.fetchedAt).toLocaleTimeString();
    const attribute = example?.attribute || 'CorrelationID';
    $('rows').innerHTML = result.rows.length ? result.rows.map(row => `<tr><td><strong>${escapeHtml(row.refId)}</strong><small>${escapeHtml(row.id)}</small></td><td class="returned-value">${valueHtml(row, attribute)}</td><td><span class="badge ${row.status==='Failed'?'bad':row.status==='Completed'?'good':''}">${escapeHtml(row.status)}</span></td><td><button class="inspect" data-detail="${escapeHtml(row.id)}">Inspect</button></td></tr>`).join('') : '<tr><td colspan="4" class="empty">Temporal returned zero matching rows. Start test executions if needed, check this run and filter, then allow processing/indexing to catch up.</td></tr>';
    const target = example || (scenario?.baseline ? scenario : null);
    const exactPreset = target && query === target.query;
    if (exactPreset) {
      const actual = result.rows.map(r=>r.refId).sort(), expected = [...target.expectedRefs].sort();
      const matches = actual.length === expected.length && actual.every((r,i)=>r===expected[i]);
      $('baselineStatus').textContent = matches ? 'Matches fresh baseline' : 'Differs from fresh baseline';
      $('baselineStatus').className = 'baseline-status ' + (matches?'match':'different');
    } else $('baselineStatus').textContent = 'Custom/mutation filter: no baseline comparison';
    $('countNote').textContent = `Count API: approximately ${result.approximateCount}. List limit: ${result.limit}. List and Count are separate reads and may temporarily differ. Baseline differences can reflect changed values, processing or indexing.`;
    notice('Live results received from Temporal. The value column is read from each returned execution, not from the illustrative example.');
  } catch (error) {
    if (version !== searchVersion || error.name === 'AbortError') return;
    $('matchCount').textContent = '-'; $('fetched').textContent = 'Live search failed';
    $('baselineStatus').textContent = 'Not verified';
    $('rows').innerHTML = '<tr><td colspan="4" class="empty">Search failed. No results or fallback sample rows are displayed.</td></tr>';
    $('countNote').textContent = 'Resolve the request error and run the live search again.';
    notice(error.message, true);
  } finally { if (version === searchVersion) { searching = false; $('searchButton').disabled = false; } }
}
async function inspect(id) {
  currentDetail = id; $('detailTitle').textContent = 'Reading execution metadata';
  $('detailContent').innerHTML = '<p>Reading Describe and, if available, a Workflow Query.</p>';
  if (!$('detailDialog').open) $('detailDialog').showModal();
  try {
    const result = await api('/api/details?id=' + encodeURIComponent(id)); if (currentDetail !== id) return;
    const e = result.execution, s = result.snapshot; $('detailTitle').textContent = e.refId;
    const attributes = Object.entries(e.attributes).map(([key,value])=>`<div class="attribute-value"><b>${escapeHtml(key)}</b><code>${escapeHtml(JSON.stringify(value))}</code></div>`).join('');
    let buttons = '';
    if (e.status === 'Running') {
      if (s?.stage === 'awaiting-review') buttons += '<button class="primary" data-action="approve">Set RequiresReview to false</button>';
      if (s?.stage === 'awaiting-release') buttons += '<button class="quiet" data-action="release">Release test execution</button>';
      buttons += '<button class="quiet" data-action="clear-description">Unset ProcessingDescription</button>';
    }
    $('detailContent').innerHTML = `<p><b>Execution status:</b> ${escapeHtml(e.status)}</p><div class="attribute-grid">${attributes}</div><div class="actions">${buttons}<button class="quiet" id="refreshDetail">Refresh metadata</button></div><p class="small">Actions send a Signal. Workflow code handles it and upserts the values; indexing follows asynchronously. Baseline counts change after these actions.</p><details class="advanced"><summary>Optional workflow query and test journey</summary>${s?`<div class="journey">${s.journey.map(j=>`<p><b>${escapeHtml(j.stage)}</b><time>${escapeHtml(new Date(j.at).toLocaleTimeString())}</time>${escapeHtml(j.explanation)}</p>`).join('')}</div>`:`<p>${escapeHtml(result.snapshotError)}</p>`}<p>${escapeHtml(result.note)}</p></details><a class="link" href="${escapeHtml(e.temporalUiUrl)}" target="_blank" rel="noopener">Open this execution in Temporal Cloud</a>`;
  } catch (error) { if (currentDetail === id) $('detailContent').textContent = error.message; }
}
async function initialise() {
  try {
    [config,catalog] = await Promise.all([api('/api/config'),api('/api/catalog')]);
    if (!Array.isArray(catalog.typeExamples)) throw new Error('This page needs the updated type-explorer backend. Stop the app, replace the source, restart, and hard-refresh.');
    $('namespace').textContent = config.namespace || 'Connection not configured';
    $('run').textContent = `Run: ${config.run} | ${config.host || 'endpoint required'}`;
    $('recordingContext').textContent = `Namespace: ${config.namespace || '(not configured)'} | Demo run: ${config.run}`;
    $('worker').textContent = config.workerRunning ? 'Worker started / live SDK' : config.workerMessage;
    $('seedButton').disabled = !config.configured;
    if (config.namespace && config.host !== 'localhost:7233' && config.host !== '127.0.0.1:7233') {
      $('cloudNamespace').href = 'https://cloud.temporal.io/namespaces/' + encodeURIComponent(config.namespace); $('cloudNamespace').hidden = false;
    }
    const typeNames = [...new Set(catalog.typeExamples.map(e=>e.type))];
    $('types').innerHTML = typeNames.map(t=>`<button class="type-chip" data-type="${escapeHtml(t)}" aria-pressed="false"><b>${escapeHtml(t)}</b><small>${escapeHtml(catalog.attributes.find(a=>a.type===t)?.dotNetType || '')}</small></button>`).join('');
    $('examples').innerHTML = catalog.typeExamples.map(e=>`<button class="example-button" data-example="${escapeHtml(e.id)}" aria-pressed="false"><b>${escapeHtml(e.type)}</b>${escapeHtml(e.attribute)}</button>`).join('');
    $('exampleCount').textContent = catalog.typeExamples.length;
    $('scenarios').innerHTML = catalog.scenarios.map(s=>`<button class="scenario" data-scenario="${escapeHtml(s.id)}">${escapeHtml(s.title)}</button>`).join('');
    selectExample(catalog.typeExamples[0].id);
    if (!config.configured) notice('Use start-demo.ps1 to enter the endpoint, namespace and API key. The page does not create workflows automatically.',true);
  } catch (error) { notice(error.message,true); $('seedButton').disabled = true; }
}
document.addEventListener('click', async event => {
  const button = event.target.closest('button'); if (!button) return;
  if (button.dataset.close) $(button.dataset.close).close();
  if (button.dataset.example) selectExample(button.dataset.example);
  if (button.dataset.type) {const example=catalog.typeExamples.find(e=>e.type===button.dataset.type);if(example)selectExample(example.id);}
  if (button.dataset.scenario) selectScenario(button.dataset.scenario);
  if (button.dataset.detail) inspect(button.dataset.detail);
  if (button.id === 'refreshDetail') inspect(currentDetail);
  if (button.dataset.action) {
    if (!confirm('Send this Signal to the test workflow? It changes the fresh baseline.')) return;
    button.disabled = true;
    try { const r = await api('/api/action',{id:currentDetail,action:button.dataset.action}); notice(r.message); await inspect(currentDetail); }
    catch (error) { notice(error.message,true); } finally { button.disabled = false; }
  }
});
$('recordingButton').addEventListener('click',()=>{const on=document.body.classList.toggle('recording');$('recordingButton').setAttribute('aria-pressed',String(on));$('recordingButton').textContent=on?'Exit recording view':'Recording view';$('recordingContext').hidden=!on;});
$('searchButton').addEventListener('click',search);
$('query').addEventListener('input',()=>{resetSearch('Filter edited. Run live search to fetch its results.');$('expected').textContent='Edited filter: no fixed expected result';});
$('seedButton').addEventListener('click',async()=>{
  if (!confirm(`Start up to 10 test workflow executions in ${config.namespace}? Existing IDs are skipped.`)) return;
  $('seedButton').disabled = true; notice('Submitting test workflow starts through the Temporal .NET SDK.');
  try {const r=await api('/api/seed',{});notice(`Started ${r.started.length}; already existed ${r.existing.length}. ${r.message}`);}
  catch(error){notice(error.message,true);}finally{$('seedButton').disabled=false;}
});
$('schemaButton').addEventListener('click',async()=>{
  try {const inspection=await api('/api/schema');showInfo(inspection.verified?'Namespace schema verified':inspection.definitionsRead?'Namespace definitions differ':'Namespace schema is not verified',`<p><b>Source:</b> ${escapeHtml(inspection.source)}</p><div class="callout">${escapeHtml(inspection.notice)}</div>${inspection.attributes.map(r=>`<div class="schema-row"><span>${escapeHtml(r.name)} / expected ${escapeHtml(r.expected)}</span><span class="${r.matches===true?'pass':r.matches===false?'fail':''}">${escapeHtml(r.actual)} ${r.matches===true?'MATCH':r.matches===false?'DIFFERENT':''}</span></div>`).join('')}`);}
  catch(error){notice(error.message,true);}
});
$('verifyButton').addEventListener('click',async()=>{
  $('verifyButton').disabled=true;notice('Comparing actual returned references with a fresh baseline. Do this before changing or unsetting values.');
  try {const r=await api('/api/verify');showInfo(r.allPassed?'Fresh baseline checks passed':'Baseline needs attention',`<p>${escapeHtml(r.note)}</p>${r.results.map(x=>`<div class="verify-result"><b class="${x.passed?'pass':'fail'}">${x.passed?'PASS':'CHECK'} / ${escapeHtml(x.title)}</b><code>Expected references: ${escapeHtml(x.expected.join(', '))}</code><code>Actual references: ${escapeHtml(x.actual.join(', '))}</code></div>`).join('')}`);}
  catch(error){notice(error.message,true);}finally{$('verifyButton').disabled=false;}
});
$('copyButton').addEventListener('click',async()=>{if(!lastScoped)return;try{await navigator.clipboard.writeText(lastScoped);notice('Exact completed query copied. Paste it into Temporal Cloud for the displayed namespace.');}catch{notice('Expand the exact query and copy it manually.',true);}});
setInterval(async()=>{
  if(document.hidden)return;
  if($('live').checked&&!searching&&!$('detailDialog').open&&!$('infoDialog').open)search();
  try{const c=await api('/api/config');$('worker').textContent=c.workerRunning?'Worker started / live SDK':c.workerMessage;}catch{}
},5000);
initialise();
