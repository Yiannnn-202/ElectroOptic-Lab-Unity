const reportData = {
    experimentName: "晶体电光效应虚拟实验报告",
    experimentDate: "2026 年 05 月 04 日",
    experimentEnvironment: "晶体电光效应虚拟仿真实验平台",
    crystal: "铌酸锂（LiNbO₃）电光晶体",
    extremeMethodHalfWaveVoltage: 300
};

document.addEventListener("DOMContentLoaded", function () {
    document.getElementById("halfWaveVoltage").textContent =
        reportData.extremeMethodHalfWaveVoltage;
});