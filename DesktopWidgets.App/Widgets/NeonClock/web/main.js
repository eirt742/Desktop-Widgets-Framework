const time=document.getElementById('time'), seconds=document.getElementById('seconds'), date=document.getElementById('date');
const days=['воскресенье','понедельник','вторник','среда','четверг','пятница','суббота'];
const months=['января','февраля','марта','апреля','мая','июня','июля','августа','сентября','октября','ноября','декабря'];
function tick(){const d=new Date();time.firstChild.textContent=d.toLocaleTimeString('ru-RU',{hour:'2-digit',minute:'2-digit',hour12:false});seconds.textContent=d.toLocaleTimeString('ru-RU',{second:'2-digit'});date.textContent=`${days[d.getDay()]}, ${d.getDate()} ${months[d.getMonth()]} ${d.getFullYear()}`;}
tick();setInterval(tick,250);