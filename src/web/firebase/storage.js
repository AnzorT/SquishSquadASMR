// Web only: @react-native-firebase/storage → Firebase's web Storage (see app.js). The one call the web SDK lacks is
// ref.putFile(localUri): here it reads the file (a blob: or data: URL from the picker) and uploads it with put().
import 'firebase/compat/storage';
import { firebase } from './app';

function withPutFile(ref) {
  ref.putFile = async (uri, meta) => {
    const blob = await (await fetch(uri)).blob();
    return ref.put(blob, meta);
  };
  return ref;
}

const storage = () => {
  const s = firebase.storage();
  return {
    ref: (p) => withPutFile(s.ref(p)),
    refFromURL: (u) => withPutFile(s.refFromURL(u)),
  };
};

export { firebase };
export default storage;
