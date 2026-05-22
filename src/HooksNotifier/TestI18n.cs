using HooksNotifier;
class Test { static void Main() {
  System.Console.WriteLine("default: " + I18n.Get("toast.idle_prompt"));
  I18n.SetLanguage("zh");
  System.Console.WriteLine("zh: " + I18n.Get("toast.idle_prompt"));
  I18n.SetLanguage("en");
  System.Console.WriteLine("en: " + I18n.Get("toast.idle_prompt"));
  System.Console.WriteLine("missing: " + I18n.Get("nonexistent.key"));
  System.Console.WriteLine("format: " + I18n.Get("toast.tool_edited", "src/main.ts"));
}}
