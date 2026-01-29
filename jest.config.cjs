module.exports = {
  testEnvironment: 'jsdom',
  transform: {
    '^.+\\.[tj]sx?$': 'babel-jest'
  },
  testPathIgnorePatterns: ["/node_modules/", "/bin/", "/obj/"]
};
