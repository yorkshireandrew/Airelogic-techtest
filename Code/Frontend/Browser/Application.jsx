const { useState } = React;

function Application() {
  const [showLanding] = useState(true);

  return (
    <div>
      <LandingPage visible={showLanding} />
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Application />);
