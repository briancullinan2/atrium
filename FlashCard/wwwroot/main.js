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

function normalizeColor(colorStr) {
    // style will always be "rgb(r, g, b)" or "rgba(r, g, b, a)"
    const matches = colorStr.match(/\d+/g);
    return matches ? matches.slice(0, 3) : [0, 0, 0];
}

window.normalizeColor = normalizeColor;

window.scrollToBottom = (elementId) => {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};


window.scrollSlightly = (elementId) => {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollTop += 10;
    }
};


window.isScrolledToBottom = (elementId) => {
    const el = document.getElementById(elementId);
    if (el) {
        return el.scrollTop > el.scrollHeight - el.clientHeight - 2;
    }
};


window.getLineHeight = (element) => {
    const style = window.getComputedStyle(element || document.body);
    return style.lineHeight; // Returns a string like "24px" or "normal"
};


window.getLineHeightInt = (element) => {
    const lineHeight = getLineHeight(element || document.body);

    if (lineHeight === 'normal') {
        // Fallback: Default browser line-height is usually ~1.2 * font-size
        const fontSize = parseFloat(style.fontSize);
        return Math.round(fontSize * 1.2);
    }

    return Math.round(parseFloat(lineHeight));
};


window.listenToResize = (dotnetHelper) => {
    window.addEventListener('resize', () => {
        dotnetHelper.invokeMethodAsync('OnWindowResize', window.innerWidth, window.innerHeight);
    });
};

