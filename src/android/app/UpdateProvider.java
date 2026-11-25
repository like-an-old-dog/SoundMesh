package app.soundmesh.audio;
import android.content.*;
import android.database.*;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.*;

public final class UpdateProvider extends ContentProvider {
    static final Uri URI=Uri.parse("content://app.soundmesh.audio.update/release.apk");
    static File apk(Context ctx){return new File(ctx.getCacheDir(),"soundmesh-release.apk");}
    public boolean onCreate(){return true;}
    private void verify(Uri uri){if(!URI.equals(uri))throw new IllegalArgumentException("Unknown update");}
    public String getType(Uri uri){verify(uri);return "application/vnd.android.package-archive";}
    public ParcelFileDescriptor openFile(Uri uri,String mode)throws FileNotFoundException{
        verify(uri);if(!"r".equals(mode))throw new FileNotFoundException("Read only");
        return ParcelFileDescriptor.open(apk(getContext()),ParcelFileDescriptor.MODE_READ_ONLY);
    }
    public Cursor query(Uri uri,String[] columns,String selection,String[] args,String sort){
        verify(uri);if(columns==null)columns=new String[]{OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE};
        MatrixCursor cursor=new MatrixCursor(columns);Object[] row=new Object[columns.length];
        for(int i=0;i<columns.length;i++){
            if(OpenableColumns.DISPLAY_NAME.equals(columns[i]))row[i]="SoundMesh.apk";
            if(OpenableColumns.SIZE.equals(columns[i]))row[i]=apk(getContext()).length();
        }
        cursor.addRow(row);return cursor;
    }
    public Uri insert(Uri u,ContentValues v){throw new UnsupportedOperationException();}
    public int update(Uri u,ContentValues v,String s,String[] a){throw new UnsupportedOperationException();}
    public int delete(Uri u,String s,String[] a){throw new UnsupportedOperationException();}
}