function timestamp() {
  return new Date().toLocaleString('ko-KR');
}

function log(tag, ...args) {
  console.log(`[${timestamp()}] [${tag}]`, ...args);
}

function warn(tag, ...args) {
  console.warn(`[${timestamp()}] [${tag}]`, ...args);
}

function error(tag, ...args) {
  console.error(`[${timestamp()}] [${tag}]`, ...args);
}

module.exports = {
  log,
  warn,
  error,
};
