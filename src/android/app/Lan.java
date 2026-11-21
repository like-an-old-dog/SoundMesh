package app.soundmesh.audio;
import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import java.net.*;
import java.util.*;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;

final class Lan {
    static final int PORT=49633;
    static final String QUERY="SOUNDMESH FIND 2";
    final SharedPreferences prefs;
    final String id,pin;
    Lan(Context ctx) {
        prefs=ctx.getSharedPreferences("pairing",0);
        synchronized(Lan.class) {
            id=prefs.getString("id",UUID.randomUUID().toString().replace("-",""));
            pin=prefs.getString("pin",String.valueOf(10000000+new SecureRandom().nextInt(90000000)));
            prefs.edit().putString("id",id).putString("pin",pin).apply();
        }
    }
    static List<InterfaceAddress> routes() {
        ArrayList<InterfaceAddress> routes=new ArrayList<>();
        try {for(NetworkInterface nic:Collections.list(NetworkInterface.getNetworkInterfaces())) {
            if(!nic.isUp()||nic.isLoopback())continue;
            for(InterfaceAddress a:nic.getInterfaceAddresses())if(a.getAddress() instanceof Inet4Address&&!a.getAddress().isLinkLocalAddress()&&a.getBroadcast()!=null) {
                if(nic.getName().startsWith("wlan")||nic.getName().startsWith("eth"))routes.add(0,a);else routes.add(a);
            }
        }}catch(Exception ignored){}
        return routes;
    }
    static String primary(){List<InterfaceAddress> list=routes();return list.isEmpty()?"":list.get(0).getAddress().getHostAddress();}
    static class Peer {
        final String id,name,host;
        Peer(String id,String name,String host){this.id=id;this.name=name;this.host=host;}
        @Override public String toString(){return name+"   ·   "+host;}
    }
    List<Peer> scan() throws Exception {
        Map<String,Peer> found=new LinkedHashMap<>();
        try(DatagramSocket udp=new DatagramSocket()){
            udp.setBroadcast(true);udp.setSoTimeout(200);
            byte[] query=QUERY.getBytes(StandardCharsets.UTF_8);
            List<InetAddress> targets=new ArrayList<>();for(InterfaceAddress r:routes())targets.add(r.getBroadcast());
            targets.add(InetAddress.getByName("255.255.255.255"));
            for(InetAddress ip:targets)try{udp.send(new DatagramPacket(query,query.length,ip,PORT));}catch(Exception ignored){}
            long until=android.os.SystemClock.elapsedRealtime()+1200;
            while(android.os.SystemClock.elapsedRealtime()<until){
                DatagramPacket pkt=new DatagramPacket(new byte[256],256);
                try{udp.receive(pkt);}catch(SocketTimeoutException e){continue;}
                String[] f=new String(pkt.getData(),0,pkt.getLength(),StandardCharsets.UTF_8).split("\\|",3);
                if(f.length==3&&f[0].equals("SM2")&&!f[1].equals(id)&&f[1].matches("[a-fA-F0-9]{32}"))
                    found.put(f[1],new Peer(f[1],f[2],pkt.getAddress().getHostAddress()));
            }
        }
        return new ArrayList<>(found.values());
    }
    void advertise(DatagramSocket udp)throws Exception {
        byte[] reply=("SM2|"+id+"|"+Build.MODEL.replace('|',' ')).getBytes(StandardCharsets.UTF_8);
        while(!udp.isClosed()){
            DatagramPacket pkt=new DatagramPacket(new byte[80],80);udp.receive(pkt);
            if(QUERY.equals(new String(pkt.getData(),0,pkt.getLength(),StandardCharsets.UTF_8)))udp.send(new DatagramPacket(reply,reply.length,pkt.getAddress(),pkt.getPort()));
        }
    }
}

