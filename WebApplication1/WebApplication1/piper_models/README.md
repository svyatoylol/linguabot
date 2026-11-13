# Модели Piper TTS (ru, pl, en)

Поместите в эту папку файлы моделей (`.onnx`) и конфигов (`.onnx.json`):

1. **Русский (ru)**:
   - `ru_RU-irina-medium.onnx`
   - `ru_RU-irina-medium.onnx.json`
2. **Польский (pl)**:
   - `pl_PL-gosia-medium.onnx`
   - `pl_PL-gosia-medium.onnx.json`
3. **Английский (en)**:
   - `en_US-lessac-medium.onnx`
   - `en_US-lessac-medium.onnx.json`

---

### Команды для быстрой загрузки (Linux / Bash):

```bash
# Русский
curl -L -O https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU/irina/medium/ru_RU-irina-medium.onnx
curl -L -O https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU/irina/medium/ru_RU-irina-medium.onnx.json

# Польский
curl -L -O https://huggingface.co/rhasspy/piper-voices/resolve/main/pl/pl_PL/gosia/medium/pl_PL-gosia-medium.onnx
curl -L -O https://huggingface.co/rhasspy/piper-voices/resolve/main/pl/pl_PL/gosia/medium/pl_PL-gosia-medium.onnx.json

# Английский
curl -L -O https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx
curl -L -O https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json
```
