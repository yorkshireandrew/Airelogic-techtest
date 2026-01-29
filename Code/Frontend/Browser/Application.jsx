const { useState } = React;

function Application() {
  const [showLanding, setShowLanding] = useState(true);
  const [showAnswer, setShowAnswer] = useState(false);
  const [answerMessage, setAnswerMessage] = useState('');

  const handleLandingResponse = (resp) => {
    if (!resp) return;
    // If the server returned a Message, show the AnswerPage with that message
    if (resp.Message && resp.Message !== '') {
      setAnswerMessage(resp.Message);
      setShowAnswer(true);
      setShowLanding(false);
    }
  };

  return (
    <div>
      <LandingPage visible={showLanding} onSubmitResponse={handleLandingResponse} />
      <AnswerPage visible={showAnswer} message={answerMessage} />
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Application />);
