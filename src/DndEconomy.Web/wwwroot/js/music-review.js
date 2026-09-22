window.musicReview = {
  start: (id) => { const audio = document.getElementById(id); if (audio) audio.play().catch(() => {}); },
  playSelection: (id, start, end) => {
    const audio = document.getElementById(id);
    if (!audio) return;
    audio.currentTime = start;
    audio.ontimeupdate = () => { if (audio.currentTime >= end) audio.pause(); };
    audio.play().catch(() => {});
  },
  stop: (id) => { const audio = document.getElementById(id); if (audio) audio.pause(); }
};
