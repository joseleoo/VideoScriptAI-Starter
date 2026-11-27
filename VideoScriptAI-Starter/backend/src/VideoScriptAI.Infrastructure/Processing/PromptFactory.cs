namespace VideoScriptAI.Infrastructure.Processing;

public static class PromptFactory
{
    public const string SystemPrompt =
        "Eres un asistente que redacta actas de reuniones. Usa el mismo idioma de la transcripción. " +
        "Devuelve únicamente el acta con estas secciones: Resumen ejecutivo, Puntos clave, Decisiones y Tareas pendientes. " +
        "Si una sección no tiene información, escribe 'No se mencionó'. No inventes datos.";

    public static string Build(string transcript) =>
        $"{SystemPrompt}\n\nTRANSCRIPCIÓN (con etiquetas de hablante cuando existan):\n{transcript}";
}
