const fs = require('node:fs');
const path = require('node:path');

function readStdin() {
  return fs.readFileSync(0, 'utf8');
}

let event;
try {
  event = JSON.parse(readStdin());
} catch (error) {
  // Do not poison the agent protocol if a runtime emits an unexpected payload.
  process.stderr.write(`AI audit hook could not parse event JSON: ${error.message}\n`);
  process.stdout.write('{}');
  process.exit(0);
}

const eventName = event.hook_event_name
  || event.eventName
  || event.event
  || 'unknown';
const sessionId = event.session_id || event.sessionId || 'unknown-session';
const timestamp = event.timestamp || new Date().toISOString();
const eventTranscriptPath = event.transcript_path || event.transcriptPath || null;
const repositoryRoot = process.cwd();
const logDirectory = path.join(repositoryRoot, 'logs');
const transcriptDirectory = path.join(logDirectory, 'copilot-transcripts');
fs.mkdirSync(logDirectory, { recursive: true });

const record = {
  timestamp,
  event: eventName,
  session_id: sessionId,
  cwd: event.cwd || repositoryRoot,
};

// Capture actual submitted prompts and relevant tool/sub-agent metadata.
const prompt = event.prompt;
if (typeof prompt === 'string') record.prompt = prompt;
const toolName = event.tool_name || event.toolName;
if (typeof toolName === 'string') record.tool_name = toolName;
if (event.tool_input !== undefined) record.tool_input = event.tool_input;
if (event.toolArgs !== undefined) record.tool_args = event.toolArgs;
if (event.tool_result !== undefined) record.tool_result = event.tool_result;
if (event.toolResult !== undefined) record.tool_result = event.toolResult;
if (event.error !== undefined) record.error = event.error;
const agentName = event.agent_name || event.agentName;
if (typeof agentName === 'string') record.agent_name = agentName;
const assistantMessage = event.last_assistant_message || event.response;
if (typeof assistantMessage === 'string') record.assistant_message = assistantMessage;
if (typeof eventTranscriptPath === 'string') record.transcript_path = eventTranscriptPath;
const initialPrompt = event.initial_prompt || event.initialPrompt;
if (typeof initialPrompt === 'string') record.initial_prompt = initialPrompt;
if (event.reason !== undefined) record.reason = event.reason;
if (event.stop_reason !== undefined) record.stop_reason = event.stop_reason;

const logPath = path.join(logDirectory, 'ai-interactions.jsonl');
const sessionPathsFile = path.join(logDirectory, '.copilot-session-paths.json');
let knownTranscriptPaths = {};
try {
  if (fs.existsSync(sessionPathsFile)) knownTranscriptPaths = JSON.parse(fs.readFileSync(sessionPathsFile, 'utf8'));
} catch { knownTranscriptPaths = {}; }
if (/agentstop|stop$/i.test(eventName) && typeof eventTranscriptPath === 'string') {
  knownTranscriptPaths[sessionId] = eventTranscriptPath;
  fs.writeFileSync(sessionPathsFile, JSON.stringify(knownTranscriptPaths, null, 2), 'utf8');
}
const transcriptPath = eventTranscriptPath || knownTranscriptPaths[sessionId] || null;
fs.appendFileSync(logPath, `${JSON.stringify(record)}\n`, 'utf8');

// The main agent's stop event carries the transcript path, but sessionEnd may not.
// Save the latest path by session ID and copy the transcript at session end so
// prompts and assistant responses can be preserved. Verify the copied file and
// review it for secrets before committing; the transcript format is runtime-owned.
if (/sessionend/i.test(eventName)) {
  try {
    if (typeof transcriptPath === 'string' && fs.existsSync(transcriptPath)) {
      fs.mkdirSync(transcriptDirectory, { recursive: true });
      const safeSessionId = String(sessionId).replace(/[^a-zA-Z0-9_-]/g, '_');
      const dateLabel = new Date().toISOString().replace(/[:.]/g, '-');
      const destination = path.join(transcriptDirectory, `${safeSessionId}-${dateLabel}.transcript`);
      fs.copyFileSync(transcriptPath, destination);
      fs.appendFileSync(logPath, `${JSON.stringify({ timestamp: new Date().toISOString(), event: 'transcript_snapshot_saved', session_id: sessionId, transcript_snapshot: path.relative(repositoryRoot, destination) })}\n`, 'utf8');
    } else {
      fs.appendFileSync(logPath, `${JSON.stringify({ timestamp: new Date().toISOString(), event: 'transcript_snapshot_missing', session_id: sessionId, transcript_path: transcriptPath })}\n`, 'utf8');
    }
    delete knownTranscriptPaths[sessionId];
    fs.writeFileSync(sessionPathsFile, JSON.stringify(knownTranscriptPaths, null, 2), 'utf8');
  } catch (error) {
    fs.appendFileSync(logPath, `${JSON.stringify({ timestamp: new Date().toISOString(), event: 'transcript_snapshot_error', session_id: sessionId, error: error.message })}\n`, 'utf8');
  }
}

// Hooks must keep stdout machine-readable; this event is observational only and
// intentionally makes no tool permission decision.
process.stdout.write('{}');
