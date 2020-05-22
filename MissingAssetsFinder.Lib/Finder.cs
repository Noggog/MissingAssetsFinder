using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MissingAssetsFinder.Lib.BSA;
using Mutagen.Bethesda.Skyrim;

namespace MissingAssetsFinder.Lib
{
    public struct MissingAsset
    {
        public ISkyrimMajorRecordGetter Record { get; set; }
        public HashSet<string> Files { get; set; }

        public MissingAsset(ISkyrimMajorRecordGetter record, string path)
        {
            Record = record;
            Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
        }
    }

    public class Finder
    {
        private readonly string _dataFolder;
        private readonly HashSet<string> _fileSet;
        public readonly List<MissingAsset> MissingAssets;

        public Finder(string dataFolder)
        {
            _dataFolder = dataFolder;
            _fileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            MissingAssets = new List<MissingAsset>();
        }

        private static readonly List<string> AllowedExtensions = new List<string>
        {
            ".nif",
            ".dds"
            //".hkx"
        };

        public async Task BuildBSACacheAsync()
        {
            Utils.Log("Start building BSA cache");
            
            var bsaList = await Task.Run(() => Directory.EnumerateFiles(_dataFolder, "*.bsa", SearchOption.TopDirectoryOnly).ToList());
            Utils.Log($"Found {bsaList.Count} BSAs in {_dataFolder}");

            bsaList.Do(async bsa =>
            {
                Utils.Log($"Start parsing BSA {bsa}");
                await using var reader = new BSAReader(bsa);

                var allowedFiles = reader.Files
                    .Where(x => AllowedExtensions.Contains(Path.GetExtension(x.Path)))
                    .Select(x => x.Path)
                    .Where(x => !_fileSet.Contains(x))
                    .ToList();

                Utils.Log($"BSA {bsa} has {allowedFiles.Count} allowed files");
                allowedFiles.Do(f => _fileSet.Add(f));

                Utils.Log($"Finished parsing BSA {bsa}");
            });

            Utils.Log($"Finished parsing of {bsaList.Count} BSAs");
        }

        public async Task BuildLooseFileCacheAsync()
        {
            Utils.Log("Start building loose file cache");

            var files = await Task.Run(() =>
                Directory.EnumerateFiles(_dataFolder, "*", SearchOption.AllDirectories)
                    .Where(x => AllowedExtensions.Contains(Path.GetExtension(x)))
                    .Select(x => x.Replace(_dataFolder, ""))
                    .Select(x => x.StartsWith("\\") ? x[1..] : x)
                    .Where(x => !_fileSet.Contains(x))
                    .ToList());

            Utils.Log($"Found {files.Count} loose files");

            files.Do(f => _fileSet.Add(f));
        }

        private bool CanAdd(string file)
        {
            return !_fileSet.Contains(file) && MissingAssets.All(x => !x.Files.Contains(file));
        }

        private void TryAdd(ISkyrimMajorRecordGetter record, string file)
        {
            if (!CanAdd(file))
                return;

            // ToLower is just for clean visualization
            file = file.ToLower();

            if (MissingAssets.Any(x => x.Record.Equals(record)))
            {
                MissingAssets.First(x => x.Record.Equals(record)).Files.Add(file);
                return;
            }

            MissingAssets.Add(new MissingAsset(record, file));
        }

        public void FindMissingAssets(string plugin)
        {
            Utils.Log($"Start finding missing assets for {plugin}");

            using var mod = SkyrimMod.CreateFromBinaryOverlay(plugin);

            Utils.Log($"Finished loading plugin {plugin}");

            mod.Armors.Records
                .Do(r =>
            {
                if (r?.WorldModel?.Female?.Model != null && !r.WorldModel.Female.Model.File.IsEmpty())
                {
                    TryAdd(r, r.WorldModel.Female.Model.File);
                }

                if (r?.WorldModel?.Male?.Model != null && !r.WorldModel.Male.Model.File.IsEmpty())
                {
                    TryAdd(r, r.WorldModel.Male.Model.File);
                }
            });

            mod.TextureSets.Records.Do(r =>
            {
                if (!r.Diffuse.IsEmpty())
                {
                    TryAdd(r, r.Diffuse!);
                }

                if (!r.NormalOrGloss.IsEmpty())
                {
                    TryAdd(r, r.NormalOrGloss!);
                }

                if (!r.EnvironmentMaskOrSubsurfaceTint.IsEmpty())
                {
                    TryAdd(r, r.EnvironmentMaskOrSubsurfaceTint!);
                }

                if (!r.GlowOrDetailMap.IsEmpty())
                {
                    TryAdd(r, r.GlowOrDetailMap!);
                }

                if (!r.Height.IsEmpty())
                {
                    TryAdd(r, r.Height!);
                }

                if (!r.Environment.IsEmpty())
                {
                    TryAdd(r, r.Environment!);
                }

                if (!r.Multilayer.IsEmpty())
                {
                    TryAdd(r, r.Multilayer!);
                }

                if (!r.BacklightMaskOrSpecular.IsEmpty())
                {
                    TryAdd(r, r.BacklightMaskOrSpecular!);
                }
            });

            mod.Weapons.Records
                .Where(r => r.Model != null)
                .Do(r =>
            {
                TryAdd(r, r.Model!.File);
            });

            mod.Statics.Records
                .Where(r => r.Model != null).Do(r =>
                {
                    TryAdd(r, r.Model!.File);
                });

            Utils.Log($"Finished finding missing assets. Found: {MissingAssets.Count} missing assets");
        }
    }
}
