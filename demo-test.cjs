// Kör: node demo-test.cjs [playwright-modul] [projektrot] [resultatmapp] [portbas] [Chromium]
const { chromium } = require(process.argv[2] || 'playwright');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(process.argv[3] || __dirname);
const output = path.resolve(process.argv[4] || path.join(root, 'demo-testresultat'));
const portBase = Number(process.argv[5] || 17100);
assert(Number.isInteger(portBase) && portBase > 1024 && portBase < 65532);
const url = n => `https://localhost:${portBase + n}`;
const basicOnly = process.argv[7] === 'basic';
fs.mkdirSync(output, { recursive: true });
const processes = [];
const results = [];
let browser;
let failure;
async function ensurePortsFree() {
  const net = require('node:net');
  for (const n of basicOnly ? [1, 2] : [1, 2, 3]) await new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once('error', reject);
    server.listen(portBase + n, () => server.close(resolve));
  });
}
function startServer(project, n) {
  const log = fs.createWriteStream(path.join(output,project+'.log'), {flags:'a'});
  const proc = spawn('dotnet',['run','--project',path.join(root,'kod',project),'--launch-profile','https','--no-build','--no-restore','--urls',url(n)], {cwd:root, windowsHide:true});
  processes.push(proc);
  proc.stdout.pipe(log); proc.stderr.pipe(log);
  proc.on('error', e => { failure = e; });
  proc.on('exit', code => { if (code && !proc.stopping) failure = Error(`${project} avslutades med kod ${code}. Se ${project}.log.`); });
  return proc;
}
async function stopServer(proc) {
  if (!proc.pid || proc.exitCode !== null) return;
  proc.stopping = true;
  if (process.platform !== 'win32') { proc.kill('SIGTERM'); return; }
  await new Promise((resolve, reject) => {
    const killer=spawn('taskkill',['/PID',String(proc.pid),'/T','/F'],{windowsHide:true});
    killer.on('exit',resolve); killer.on('error',reject);
  });
}
function pass(name, detail) { results.push({ name, detail }); console.log('OK: ' + name + ' — ' + detail); }
async function until(check, timeout = 20000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await check()) return; await new Promise(r => setTimeout(r, 100)); }
  throw Error('Timeout');
}
async function connected(page) { await page.waitForFunction(() => window.connection?.state === 'Connected'); }
async function invoke(page, method, ...args) {
  return page.evaluate(async ({method,args}) => {
    try { await window.connection.invoke(method,...args); return 'OK'; }
    catch (e) { return e.message; }
  }, {method,args});
}
async function login(page, name) {
  await page.goto(url(3));
  await page.locator('#login-name').fill(name);
  await page.locator('#login-form button[type=submit]').click();
  await connected(page);
}
async function checkStart(ctx) {
  const start = await ctx.newPage(); await start.goto(url(1));
  await start.getByText('Fyll i TODO:erna för att ansluta',{exact:true}).waitFor();
  await start.screenshot({path:path.join(output,'01-start.png'),fullPage:true});
  assert.equal(await start.evaluate(()=>window.connection),null);
  pass('Startprojekt','Sidan laddas, connection är null och statusen visar att TODO:erna återstår.');
}
async function checkWeakRestart(ctx,a) {
  await stopServer(processes[1]);
  await a.waitForFunction(()=>connection.state==='Disconnected');
  startServer('02-svag-hub',2);
  const ready = await ctx.newPage();
  await until(async()=>{try{return (await ready.goto(url(2))).ok();}catch{return false;}});
  await connected(ready);
  assert.equal(await a.evaluate(()=>connection.state),'Disconnected');
  await a.reload(); await connected(a);
  pass('Svag: serveromstart','02 förblir frånkopplad utan automatic reconnect; omladdning ansluter igen.');
}
(async () => {
  await ensurePortsFree();
  for(const project of ['01-start','02-svag-hub','03-hardad-hub']) fs.writeFileSync(path.join(output,project+'.log'),'');
  startServer('01-start',1);
  startServer('02-svag-hub',2);
  if (!basicOnly) startServer('03-hardad-hub',3);
  // Inget ignoreHTTPSErrors: testet kräver att certifikatet är betrott.
  browser = await chromium.launch({headless:true, ...(process.argv[6] ? {executablePath:process.argv[6]} : {})});
  const ctx = await browser.newContext({viewport:{width:1280,height:900}});
  const a = await ctx.newPage(), b = await ctx.newPage();
  for(const n of basicOnly ? [1,2] : [1,2,3]) await until(async () => { if(failure) throw failure; try { return (await a.goto(url(n))).ok(); } catch { return false; } });
  const cdp = await ctx.newCDPSession(a);
  await cdp.send('Network.enable');
  const handshakes = [], frames = [], responses = [];
  cdp.on('Network.webSocketHandshakeResponseReceived', e => handshakes.push(e.response.status));
  cdp.on('Network.webSocketFrameSent', e => frames.push(e.response.payloadData));
  a.on('response', r => { if(r.url().includes('/negotiate')) responses.push(r.status()); });
  await a.goto(url(2)); await b.goto(url(2));
  await connected(a); await connected(b);
  await a.locator('#username').fill('Alice'); await a.locator('#message').fill('Hej Bob!');
  await a.locator('#send-form button').click();
  await b.getByText('Alice: Hej Bob!',{exact:true}).waitFor();
  await a.getByText('Alice: Hej Bob!',{exact:true}).waitFor();
  pass('Tvåflikschatt över HTTPS', 'Formuläret skickar till båda flikarna, även avsändaren.');
  console.log('Network:', JSON.stringify({responses,handshakes,frames:frames.slice(0,3)}));
  assert(responses.includes(200)); assert(handshakes.includes(101)); assert(frames.some(f=>f.includes('SendMessage')));
  pass('Slide 20: negotiate, 101 och frames','POST-svar 200, WebSocket-status 101 och SendMessage-frame observerade.');
  await invoke(a,'SendMessage','Administrator','Spoofing fungerar');
  await b.getByText('Administrator: Spoofing fungerar',{exact:true}).waitFor();
  pass('Svag: spoofing','Bob ser Administrator som avsändare.');
  assert.equal(await invoke(a,'JoinGroup','Administrators'),'OK');
  assert.equal(await invoke(b,'JoinGroup','Administrators'),'OK');
  await invoke(b,'SendToGroup','Administrators','admin','Gruppattack fungerar');
  await a.getByText('[Administrators] admin: Gruppattack fungerar',{exact:true}).waitFor();
  pass('Svag: gruppåtkomst','Båda går med utan inloggning; falskt adminmeddelande når gruppen.');
  await a.screenshot({path:path.join(output,'02-svag-chatt.png'),fullPage:true});
  const dialogs = [];
  for(const page of [a,b]) page.on('dialog', async d => { dialogs.push(d.message()); await d.dismiss(); });
  await invoke(a,'SendMessage','Eve',"<img src=x onerror=alert('xss')>");
  await until(()=>dialogs.length===2); assert(dialogs.every(x=>x==='xss'));
  pass('Svag: XSS','alert("xss") utlöstes i båda riktiga webbläsarflikarna.');
  assert.equal(await invoke(a,'SendMessage','Eve','B'.repeat(3000)),'OK');
  await until(async()=> (await b.locator('#messages').innerText()).includes('B'.repeat(3000)));
  pass('Svag: 3000 tecken','Hela meddelandet visas hos mottagaren.');
  assert.notEqual(await invoke(a,'SendMessage','Eve','A'.repeat(1024*1024)),'OK');
  await a.waitForFunction(()=>window.connection.state==='Disconnected');
  pass('Svag: 1 MB','Anropet misslyckas och anslutningen stängs.');
  await a.reload(); await connected(a);
  const spam = await a.evaluate(async()=>{
    const r=await Promise.allSettled(Array.from({length:1000},(_,i)=>connection.invoke('SendMessage','Eve','spam '+i)));
    return r.filter(x=>x.status==='fulfilled').length;
  });
  assert.equal(spam,1000);
  await until(async()=>await b.locator('#messages li').filter({hasText:/^Eve: spam /}).count()===1000);
  pass('Svag: spam','Alla 1000 anrop lyckas och visas hos mottagaren.');
  if (basicOnly) { await checkStart(ctx); await checkWeakRestart(ctx,a); return; }
  const aliceContext = await browser.newContext({viewport:{width:1280,height:900}});
  const adminContext = await browser.newContext();
  const alice = await aliceContext.newPage(), admin = await adminContext.newPage();
  await alice.goto(url(3));
  const unauth = await alice.evaluate(async()=>(await fetch('/hubs/chat/negotiate?negotiateVersion=1',{method:'POST'})).status);
  assert.equal(unauth,401);
  await login(alice,'Alice'); await login(admin,'admin');
  pass('Härdad: inloggning','Anonym negotiate får 401; Alice och admin ansluter i separata sessioner.');
  assert.notEqual(await invoke(alice,'SendMessage','Administrator','hej'),'OK');
  assert.equal(await invoke(alice,'SendMessage','Identitet från servern'),'OK');
  await admin.getByText('Alice: Identitet från servern',{exact:true}).waitFor();
  pass('Härdad: avsändare','Gammal signatur avvisas; korrekt anrop visas som Alice.');
  assert.match(await invoke(alice,'JoinGroup','Administrators'),/behörighet/);
  assert.match(await invoke(alice,'SendToGroup','Administrators','hej'),/behörighet/);
  assert.equal(await invoke(admin,'JoinGroup','Administrators'),'OK');
  assert.equal(await invoke(alice,'JoinGroup','General'),'OK');
  assert.equal(await invoke(admin,'JoinGroup','General'),'OK');
  assert.equal(await invoke(alice,'SendToGroup','General','Hej gruppen'),'OK');
  await admin.getByText('[General] Alice: Hej gruppen',{exact:true}).waitFor();
  pass('Härdad: grupprättigheter','Alice nekas både inträde och direkt sändning till Administrators. Admin släpps in. General fungerar.');
  assert.match(await invoke(alice,'Broadcast','Obehörigt utskick'),/unauthorized/);
  assert.equal(await alice.locator('#messages').filter({hasText:'Obehörigt utskick'}).count(),0);
  pass('Härdad: Broadcast nekas','Vanlig användare nekas av Admin-policyn före metodens kod.');
  assert.equal(await invoke(admin,'Broadcast','Admin skickar till alla'),'OK');
  await alice.getByText('Meddelande från admin: Admin skickar till alla',{exact:true}).waitFor();
  await admin.getByText('Meddelande från admin: Admin skickar till alla',{exact:true}).waitFor();
  pass('Härdad: Broadcast tillåts','Admin når båda sessionerna med ett systemmeddelande.');
  let hardDialogs=0;
  for(const page of [alice,admin]) page.on('dialog',async d=>{hardDialogs++;await d.dismiss();});
  const payload="<img src=x onerror=alert('xss')>";
  assert.equal(await invoke(alice,'SendMessage',payload),'OK');
  await admin.getByText('Alice: '+payload,{exact:true}).waitFor();
  assert.equal(await admin.locator('#messages img').count(),0); assert.equal(hardDialogs,0);
  pass('Härdad: XSS','Payload visas bokstavligt; inget img-element skapas och ingen dialog öppnas.');
  assert.match(await invoke(alice,'SendMessage','B'.repeat(3000)),/500/);
  assert.match(await invoke(alice,'SendMessage','   '),/tomt/);
  pass('Härdad: validering','3000 tecken ger 500-teckensfel; blank text avvisas.');
  await alice.screenshot({path:path.join(output,'03-hardad-chatt.png'),fullPage:true});
  const originalConnectionId = await alice.evaluate(()=>connection.connectionId);
  await alice.evaluate(()=>{
    window.demoTransportClosed=false;
    connection.onreconnecting(()=>{window.demoTransportClosed=true;});
    connection.onclose(()=>{window.demoTransportClosed=true;});
  });
  assert.notEqual(await invoke(alice,'SendMessage','A'.repeat(1024*1024)),'OK');
  await alice.waitForFunction(()=>window.demoTransportClosed);
  pass('Härdad: 1 MB','Transporten stängs; klientens automatiska återanslutning aktiveras.');
  await connected(alice);
  assert.notEqual(await alice.evaluate(()=>connection.connectionId),originalConnectionId);
  assert.equal(await invoke(admin,'SendToGroup','General','Gruppen före återinträde'),'OK');
  assert.equal(await invoke(admin,'SendMessage','Kontroll efter grupputskick'),'OK');
  await alice.getByText('admin: Kontroll efter grupputskick',{exact:true}).waitFor();
  assert.equal(await alice.getByText('[General] admin: Gruppen före återinträde',{exact:true}).count(),0);
  assert.equal(await invoke(alice,'JoinGroup','General'),'OK');
  assert.equal(await invoke(admin,'SendToGroup','General','Gruppen efter återinträde'),'OK');
  await alice.getByText('[General] admin: Gruppen efter återinträde',{exact:true}).waitFor();
  pass('Härdad: reconnect och grupper','Ny connectionId efter 1 MB; General når Alice först när hon gått med igen.');
  await login(alice,'Alice');
  const rates = await alice.evaluate(async()=>{
    const r=await Promise.allSettled(Array.from({length:30},(_,i)=>connection.invoke('SendMessage','spam '+i)));
    return {ok:r.filter(x=>x.status==='fulfilled').length,errors:r.filter(x=>x.status==='rejected').map(x=>x.reason.message)};
  });
  assert.equal(rates.ok,20); assert.equal(rates.errors.length,10); assert(rates.errors.every(e=>e.includes('För många anrop')));
  pass('Härdad: rate limiting','Ny anslutning: 20 av 30 anrop lyckas, 10 nekas med För många anrop.');
  await login(alice,'Alice');
  const secondContext = await browser.newContext();
  const observerContext = await browser.newContext();
  const secondAlice = await secondContext.newPage(), observer = await observerContext.newPage();
  await login(secondAlice,'Alice'); await login(observer,'Charlie');
  assert.equal(await invoke(admin,'SendPrivate','ALICE','Bara till Alice'),'OK');
  for(const page of [alice,secondAlice]) await page.getByText('(privat) admin: Bara till Alice',{exact:true}).waitFor();
  await admin.getByText('(privat) admin till ALICE: Bara till Alice',{exact:true}).waitFor();
  assert.equal(await invoke(admin,'SendMessage','Privattest klart'),'OK');
  await observer.getByText('admin: Privattest klart',{exact:true}).waitFor();
  assert.equal(await observer.getByText('(privat) admin: Bara till Alice',{exact:true}).count(),0);
  pass('Härdad: Clients.User','Privat meddelande når två Alice-anslutningar samt kvitto till admin; Charlie får inget.');
  await alice.locator('#message').fill('UI fungerar');
  await alice.locator('#send-form button').click();
  await observer.getByText('Alice: UI fungerar',{exact:true}).waitFor();
  await alice.locator('#logout').click();
  await alice.getByText('Ej inloggad',{exact:true}).waitFor();
  assert.equal(await alice.evaluate(async()=>(await fetch('/me')).status),401);
  assert.equal(await alice.locator('#message').isDisabled(),true);
  assert.equal(await alice.evaluate(()=>connection.state),'Disconnected');
  pass('Härdad: formulär och utloggning','Formuläret skickar; logout stoppar anslutningen, tar bort cookie och stänger av chatten.');
  await login(alice,'Alice');
  await admin.close(); await secondContext.close(); await observerContext.close();
  const beforeRestart=await alice.evaluate(()=>connection.connectionId);
  await stopServer(processes[2]);
  await alice.waitForFunction(()=>connection.state==='Reconnecting');
  startServer('03-hardad-hub',3);
  await connected(alice);
  assert.notEqual(await alice.evaluate(()=>connection.connectionId),beforeRestart);
  assert.equal(await invoke(alice,'SendMessage','Efter serveromstart'),'OK');
  await alice.getByText('Alice: Efter serveromstart',{exact:true}).waitFor();
  pass('Härdad: serveromstart','Efter stopp och omstart återansluter klienten med ny connectionId och kan skicka.');
  await checkWeakRestart(ctx,a);
  await checkStart(ctx);
})().catch(e=>{failure=e;console.error(e);process.exitCode=1;}).finally(async()=>{
  fs.writeFileSync(path.join(output,'resultat.json'),JSON.stringify({date:new Date().toISOString(),browser:browser?.version(),portBase,scope:basicOnly?'basic':'all',httpsErrorsIgnored:false,success:!failure,error:failure?.message,results},null,2));
  if(browser) await browser.close();
  // Stäng endast processerna som detta test startade, inklusive dotnet-barnen.
  for(const proc of processes) await stopServer(proc);
});
