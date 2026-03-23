const { onRequest } = require("firebase-functions/v2/https");

exports.generateWords = onRequest(
  { secrets: ["OPENAI_API_KEY"] },
  async (req, res) => {
    res.set("Access-Control-Allow-Origin", "*");

    if (req.method === "OPTIONS") {
      res.set("Access-Control-Allow-Methods", "POST");
      res.set("Access-Control-Allow-Headers", "Content-Type");
      res.status(204).send("");
      return;
    }

    try {
      const { prompt, temperature, maxTokens } = req.body;

      const response = await fetch("https://api.openai.com/v1/chat/completions", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Authorization": `Bearer ${process.env.OPENAI_API_KEY}`
        },
        body: JSON.stringify({
          model: "gpt-4o-mini",
          temperature: temperature || 1.0,
          max_tokens: maxTokens || 1024,
          messages: [
            {
              role: "system",
              content: "You are a Korean word generator for a drawing game. Respond ONLY with valid JSON. No other text."
            },
            { role: "user", content: prompt }
          ]
        })
      });

      const data = await response.json();
      res.json(data);

    } catch (error) {
      console.error("Error:", error);
      res.status(500).json({ error: error.message });
    }
  }
);

// ↓ 여기부터 추가
exports.recognizeSpeech = onRequest(
  { secrets: ["GOOGLE_SPEECH_API_KEY"] },
  async (req, res) => {
    res.set("Access-Control-Allow-Origin", "*");

    if (req.method === "OPTIONS") {
      res.set("Access-Control-Allow-Methods", "POST");
      res.set("Access-Control-Allow-Headers", "Content-Type");
      res.status(204).send("");
      return;
    }

    try {
      const { audio } = req.body;

      if (!audio) {
        res.status(400).json({ error: "audio field is required" });
        return;
      }

      const response = await fetch(
        `https://speech.googleapis.com/v1/speech:recognize?key=${process.env.GOOGLE_SPEECH_API_KEY}`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            config: {
              encoding: "LINEAR16",
              sampleRateHertz: 16000,
              languageCode: "ko-KR"
            },
            audio: { content: audio }
          })
        }
      );

      const data = await response.json();
      res.json(data);

    } catch (error) {
      console.error("recognizeSpeech error:", error);
      res.status(500).json({ error: error.message });
    }
  }
);