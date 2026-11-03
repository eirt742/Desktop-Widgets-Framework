const area=document.getElementById('text'),save=document.getElementById('save'),count=document.getElementById('count');const KEY='desktopwidgets.quicknotes.text';
try{area.value=localStorage.getItem(KEY)||'';}catch(e){}
function update(){count.textContent=`${area.value.length} символов`;save.textContent='● СОХРАНЕНО';}
area.addEventListener('input',()=>{count.textContent=`${area.value.length} символов`;save.textContent='● СОХРАНЯЮ…';try{localStorage.setItem(KEY,area.value);save.textContent='● СОХРАНЕНО';}catch(e){save.textContent='● НЕ СОХРАНЕНО';}});
document.getElementById('clear').onclick=()=>{if(area.value.trim()&&!confirm('Удалить текст заметки?'))return;area.value='';try{localStorage.removeItem(KEY);}catch(e){}update();area.focus();};
document.getElementById('today').textContent=new Date().toLocaleDateString('ru-RU',{weekday:'long',day:'numeric',month:'long'});update();