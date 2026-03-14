const getThemeColors = () => {
    const sensor = document.getElementById('theme-sensor');
    if (!sensor) return {
        stone: 'rgba(63, 98, 18, 0.5)',
        nature: '#10b981',
        electric: '#67e8f9',
        gold: '#fbbf24'
    };
    const getStyle = (cls) => window.getComputedStyle(sensor.querySelector(cls)).color;
    return {
        stone: getStyle('.sensor-stone'),
        main: getStyle('.sensor-main'),
        nature: getStyle('.sensor-nature'),
        electric: getStyle('.sensor-electric'),
        gold: getStyle('.sensor-gold')
    };
};

window.getThemeColors = getThemeColors;

