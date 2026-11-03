const display=document.getElementById('display'),progress=document.getElementById('progress'),toggle=document.getElementById('toggle'),status=document.getElementById('status'),sessions=document.getElementById('sessions');
const modes=[...document.querySelectorAll('.mode')];const circumference=2*Math.PI*96;progress.style.strokeDasharray=circumference;
let total=1500,left=total,running=false,handle=null,count=0;
function render(){display.textContent=`${String(Math.floor(left/60)).padStart(2,'0')}:${String(left%60).padStart(2,'0')}`;progress.style.strokeDashoffset=circumference*(1-left/total);}
function stop(){running=false;clearInterval(handle);toggle.textContent='▶ Продолжить';}
function tick(){if(left>0){left--;render();}else{stop();status.textContent='СЕССИЯ ЗАВЕРШЕНА ✦';count++;sessions.textContent=`${count} СЕССИЙ`;toggle.textContent='▶ Ещё раз';}}
toggle.onclick=()=>{if(running){stop();status.textContent='ПАУЗА';}else{if(left===0)left=total;running=true;status.textContent='ИДЁТ ФОКУС-СЕССИЯ';toggle.textContent='Ⅱ Пауза';handle=setInterval(tick,1000);}};
document.getElementById('reset').onclick=()=>{stop();left=total;status.textContent='ГОТОВ К СТАРТУ';toggle.textContent='▶ Начать';render();};
modes.forEach(b=>b.onclick=()=>{stop();modes.forEach(x=>x.classList.remove('active'));b.classList.add('active');total=Number(b.dataset.min)*60;left=total;status.textContent='ГОТОВ К СТАРТУ';toggle.textContent='▶ Начать';render();});
render();