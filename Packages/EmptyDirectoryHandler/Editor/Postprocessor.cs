//#define EMPTY_DIRECTORY_HANDLER_DISABLE_ERROR_LOG
//#define EMPTY_DIRECTORY_HANDLER_ENABLE_VERBOSE_LOG
//#define EMPTY_DIRECTORY_HANDLER_ENABLE_ALL_PLACES

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;


namespace Fiftytwo.EmptyDirectoryHandler
{
    internal class Postprocessor : AssetPostprocessor
    {
        private static readonly char SystemDirectorySeparator = Path.DirectorySeparatorChar;
        private const char UnityDirectorySeparator = '/';
        private const string EmptyDirectoryMarkerName = ".empty_directory";
        private const string EmptyDirectoryMarkerPathSuffix = "/" + EmptyDirectoryMarkerName;

        private static StringBuilder _sb = new StringBuilder();


        private static void OnPostprocessAllAssets (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths )
        {
            var directories = new Dictionary<string, DirectoryState>();

            int i;
            int count = importedAssets.Length;
            for( i = 0; i < count; ++i )
                PostprocessPath( importedAssets[i], false, directories );

            count = movedAssets.Length;
            for( i = 0; i < count; ++i )
                PostprocessPath( movedAssets[i], false, directories );

            int idx;
            string path, dirPath;

            count = deletedAssets.Length;
            for( i = 0; i < count; ++i )
            {
                path = deletedAssets[i];
                idx = path.LastIndexOf( UnityDirectorySeparator );
                if( idx <= 0 )
                    continue;
                dirPath = path.Substring( 0, idx );
                PostprocessPath( dirPath, true, directories );
            }

            count = movedFromAssetPaths.Length;
            for( i = 0; i < count; ++i )
            {
                path = movedFromAssetPaths[i];
                idx = path.LastIndexOf( UnityDirectorySeparator );
                if( idx <= 0 )
                    continue;
                dirPath = path.Substring( 0, idx );
                PostprocessPath( dirPath, true, directories );
            }
        }

        private static void PostprocessPath ( string path, bool mayBeMissing, Dictionary<string, DirectoryState> directories )
        {
            DirectoryState directoryState;

#if !EMPTY_DIRECTORY_HANDLER_ENABLE_ALL_PLACES
            if( !path.StartsWith( "Assets/" ) )
                return;
#endif

            if( AssetDatabase.IsValidFolder( path ) )
            {
                if( mayBeMissing && !Directory.Exists( path ) )
                    return;

                if( !directories.TryGetValue( path, out directoryState ) ||
                    directoryState <= DirectoryState.NonEmptyNotProcessed )
                {
                    if( directoryState == DirectoryState.NotProcessed && IsDirectoryEmpty( path ) )
                    {
                        directories[path] = DirectoryState.EmptyProcessed;
                        SetDirectoryEmpty( path );
                    }
                    else
                    {
                        directories[path] = DirectoryState.NonEmptyProcessed;
                        SetDirectoryNotEmpty( path );
                    }
                }
            }
            else if( AssetDatabase.AssetPathToGUID( path ).Length == 0 )
            {
                return;
            }

            var idx = path.LastIndexOf( UnityDirectorySeparator );
            if( idx <= 0 )
                return;
            var dirPath = path.Substring( 0, idx );

            if( mayBeMissing && !Directory.Exists( dirPath ) )
                return;
            
            if( !directories.TryGetValue( dirPath, out directoryState ) || directoryState <= DirectoryState.NonEmptyNotProcessed )
            {
                directories[dirPath] = DirectoryState.NonEmptyProcessed;
                SetDirectoryNotEmpty( dirPath );
                SetParentDirectoryTreeNonEmptyNotProcessed( dirPath, directories );
            }
        }

        // Check the directory for emptiness according to the Unity rules where some special folder and file names
        // are invisible: https://docs.unity3d.com/Manual/SpecialFolders.html
        private static bool IsDirectoryEmpty ( string dirPath )
        {
            try
            {
                int idx;

                string name;
                int nameLength;

                foreach( var path in Directory.EnumerateFiles( dirPath ) )
                {
                    idx = path.LastIndexOf( SystemDirectorySeparator );
                    name = idx >= 0 ? path.Substring( idx + 1 ) : path;
                    nameLength = name.Length;

                    if( nameLength == 0 || name[0] == '.' || name[nameLength - 1] == '~' )
                        continue;
                    if( string.Equals( name, "cvs", StringComparison.OrdinalIgnoreCase ) )
                        continue;

                    if( name.EndsWith( ".tmp", StringComparison.OrdinalIgnoreCase ) )
                        continue;

                    return false;
                }

                foreach( var path in Directory.EnumerateDirectories( dirPath ) )
                {
                    idx = path.LastIndexOf( SystemDirectorySeparator );
                    name = idx >= 0 ? path.Substring( idx + 1 ) : path;
                    nameLength = name.Length;

                    if( nameLength == 0 || name[0] == '.' || name[nameLength - 1] == '~' )
                        continue;
                    if( string.Equals( name, "cvs", StringComparison.OrdinalIgnoreCase ) )
                        continue;

                    if( ( new DirectoryInfo( path ).Attributes & FileAttributes.Hidden ) != 0 )
                        continue;

                    return false;
                }
            }
            catch( Exception ex )
            {
#if !EMPTY_DIRECTORY_HANDLER_DISABLE_ERROR_LOG
                Debug.LogException( ex );
#endif
                return false;
            }

            return true;
        }

        private static void SetParentDirectoryTreeNonEmptyNotProcessed ( string dirPath, Dictionary<string, DirectoryState> directories )
        {
            var idx = dirPath.LastIndexOf( UnityDirectorySeparator );
            while( idx > 0 )
            {
                dirPath = dirPath.Substring( 0, idx );
                if( directories.TryGetValue( dirPath, out var state ) )
                {
                    if( state == DirectoryState.NotProcessed )
                        directories[dirPath] = DirectoryState.NonEmptyNotProcessed;
                    else
                        return;
                }
                else
                {
                    directories[dirPath] = DirectoryState.NonEmptyNotProcessed;
                }

                idx = dirPath.LastIndexOf( UnityDirectorySeparator, idx - 1 );
            }
        }

        private static void SetDirectoryEmpty ( string dirPath )
        {
            FileStream fileStream = null;

            try
            {
                _sb.Length = 0;
                _sb.Append( dirPath );
                _sb.Append( EmptyDirectoryMarkerPathSuffix );

                var filePath = _sb.ToString();

#if EMPTY_DIRECTORY_HANDLER_ENABLE_VERBOSE_LOG
                if( File.Exists( filePath ) )
                    Debug.Log( $"ALREADY EXISTS: {filePath}" );
                else
                    Debug.Log( $"CREATE: {filePath}" );
#endif

                fileStream = File.Open( filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read );
            }
            catch( Exception ex )
            {
#if !EMPTY_DIRECTORY_HANDLER_DISABLE_ERROR_LOG
                Debug.LogException( ex );
#endif
            }
            finally
            {
                fileStream?.Close();
            }
        }

        private static void SetDirectoryNotEmpty ( string dirPath )
        {
            try
            {
                _sb.Length = 0;
                _sb.Append( dirPath );
                _sb.Append( EmptyDirectoryMarkerPathSuffix );

                var filePath = _sb.ToString();

#if EMPTY_DIRECTORY_HANDLER_ENABLE_VERBOSE_LOG
                if( File.Exists( filePath ) )
                    Debug.Log( $"DELETE: {filePath}" );
                else
                    Debug.Log( $"ALREADY ABSENT: {filePath}" );
#endif

                File.Delete( _sb.ToString() );
            }
            catch( Exception ex )
            {
#if !EMPTY_DIRECTORY_HANDLER_DISABLE_ERROR_LOG
                Debug.LogException( ex );
#endif
            }
        }


        private enum DirectoryState
        {
            NotProcessed,
            NonEmptyNotProcessed,
            EmptyProcessed,
            NonEmptyProcessed
        }
    }
}
